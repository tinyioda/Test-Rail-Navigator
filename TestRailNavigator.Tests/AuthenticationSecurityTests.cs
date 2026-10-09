using System.Net;
using System.Text;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Covers the per-user, live-TestRail-validated sign-in boundary through the actual Razor Pages request pipeline.</summary>
public class AuthenticationSecurityTests
{
    private const string Username = "fixture-user@example.test";
    private const string Password = "fixture-only-not-a-real-password";

    /// <summary>Every data page and mutation handler must challenge before contacting integrations.</summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task AnonymousRequestsCannotAccessApplicationHandlers(string method)
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var factory = new SecurityWebApplicationFactory(scope);
        using var browser = factory.CreateBrowser();
        string[] paths =
        [
            "/", "/Project/1", "/Milestones/1", "/PlanDetail/1", "/Tests/1",
            "/TestDetail/1", "/TestCaseEdit/1", "/GenerateCases",
            "/GenerateCases?handler=Load", "/GenerateCases?handler=Confirm",
            "/GenerateHierarchy",
            "/CreatePlanFromStory?handler=Confirm",
            "/PlanDetail/1?handler=Results&testId=1", "/Tests/1?handler=QuickEdit", "/Logout"
        ];

        foreach (var path in paths)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            request.Headers.Add("Cookie", "SetupAuthenticated=true");
            using var response = await browser.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("https://localhost/Login", response.Headers.Location?.AbsoluteUri);
        }

        // /Setup is publicly routable (anonymous) so a fresh deployment can reach it, but once the
        // TestRail connection is configured the gate bounces every request -- anonymous or not --
        // straight to the dashboard instead of exposing the page at all.
        foreach (var setupPath in new[] { "/Setup", "/Setup?handler=Login" })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), setupPath);
            request.Headers.Add("Cookie", "SetupAuthenticated=true");
            using var response = await browser.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/", response.Headers.Location?.OriginalString);
        }

        Assert.Equal(0, factory.OutboundRequests);
    }

    /// <summary>Blank credentials are rejected without ever contacting TestRail, even when the
    /// connection is fully configured -- there is no point validating input that is trivially invalid.</summary>
    [Theory]
    [InlineData("", "")]
    [InlineData("someone", "")]
    [InlineData("", "password")]
    public async Task MissingCredentialsFailClosedWithoutContactingTestRail(string username, string password)
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var factory = new SecurityWebApplicationFactory(scope);
        var auth = factory.Services.GetRequiredService<AdminAuthenticationService>();

        Assert.True(await auth.IsConfiguredAsync());
        Assert.Null(await auth.AuthenticateAsync(username, password));
        Assert.Equal(0, factory.OutboundRequests);
    }

    /// <summary>Before the TestRail connection exists, nobody can sign in yet: /Setup must be the
    /// only reachable page, and it must be reachable without signing in first (there is no live
    /// connection against which to validate anyone's credentials).</summary>
    [Fact]
    public async Task UnconfiguredConnectionExposesOnlySetupWithoutAuthentication()
    {
        using var scope = await TestSettingsScope.CreateAsync(new TestRailSettings());
        using var factory = new SecurityWebApplicationFactory(scope);
        using var browser = factory.CreateBrowser();

        using var setup = await browser.GetAsync("/Setup");
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);

        string[] paths = ["/", "/Login", "/Project/1", "/Milestones/1", "/PlanDetail/1"];
        foreach (var path in paths)
        {
            using var response = await browser.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Setup", response.Headers.Location?.OriginalString);
        }

        using var health = await browser.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        Assert.Equal(0, factory.OutboundRequests);
    }

    /// <summary>Until the TestRail connection is configured, every page (anonymous or not) is gated to
    /// /Setup; once configured, /Setup itself becomes unreachable and bounces to the dashboard.</summary>
    [Fact]
    public async Task ConnectionGateControlsSetupReachability()
    {
        using (var scope = await TestSettingsScope.CreateAsync(new TestRailSettings()))
        {
            using var factory = new SecurityWebApplicationFactory(scope);
            using var browser = factory.CreateBrowser();

            // Nobody can sign in yet to reach "/" -- the connection gate redirects straight to Setup
            // before authentication even runs.
            using var home = await browser.GetAsync("/");
            Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
            Assert.Equal("/Setup", home.Headers.Location?.OriginalString);

            using var setup = await browser.GetAsync("/Setup");
            Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        }

        using var configuredScope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var configuredFactory = new SecurityWebApplicationFactory(configuredScope)
        {
            TestRailResponder = _ => ValidUserResponse()
        };
        using var configuredBrowser = configuredFactory.CreateBrowser();
        using var configuredSignIn = await SignInAsync(configuredBrowser, "/");
        using var configuredHome = await configuredBrowser.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, configuredHome.StatusCode);

        using var configuredSetup = await configuredBrowser.GetAsync("/Setup");
        Assert.Equal(HttpStatusCode.Redirect, configuredSetup.StatusCode);
        Assert.Equal("/", configuredSetup.Headers.Location?.OriginalString);
    }

    /// <summary>Sign-in validates credentials live against TestRail, is antiforgery-protected,
    /// generic on failure, and issues a secure cookie carrying no credential material.</summary>
    [Fact]
    public async Task SignInRequiresAntiforgeryAndValidCredentials()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var factory = new SecurityWebApplicationFactory(scope);
        using var browser = factory.CreateBrowser();
        using var withoutToken = await browser.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = Username,
            ["Password"] = Password
        }));
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);

        factory.TestRailResponder = _ => InvalidCredentialsResponse();
        using var invalid = await SignInAsync(browser, "/Setup", password: "incorrect-fixture-password");
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Contains("Invalid username or password.", await invalid.Content.ReadAsStringAsync());
        Assert.DoesNotContain("incorrect-fixture-password", await invalid.Content.ReadAsStringAsync());

        factory.TestRailResponder = _ => ValidUserResponse();
        using var valid = await SignInAsync(browser, "/");
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);
        var cookie = Assert.Single(valid.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("TestRailNavigator.Auth=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, cookie);

        // The TestRail connection is configured in this fixture, so /Setup is no longer reachable.
        using var setup = await browser.GetAsync("/Setup");
        Assert.Equal(HttpStatusCode.Redirect, setup.StatusCode);
        Assert.Equal("/", setup.Headers.Location?.OriginalString);

        using var home = await browser.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.True(home.Headers.CacheControl?.NoStore);
        Assert.Contains("Sign out", await home.Content.ReadAsStringAsync());
    }

    /// <summary>The public login page cannot disclose the shared debug console.</summary>
    [Fact]
    public async Task LoginDoesNotRenderPrivateConsoleMessages()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var factory = new SecurityWebApplicationFactory(scope);
        using var browser = factory.CreateBrowser();
        factory.Services.GetRequiredService<ConsoleLogService>().Log("private-console-fixture");

        using var response = await browser.GetAsync("/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-console-fixture", html);
        Assert.DoesNotContain("consoleOutput", html);
    }

    /// <summary>Public liveness reports server health without exposing integrations or requiring sign-in.</summary>
    [Fact]
    public async Task HealthProbeIsPublicAndDoesNotContactIntegrations()
    {
        using var scope = await TestSettingsScope.CreateAsync(new TestRailSettings());
        using var factory = new SecurityWebApplicationFactory(scope);
        using var browser = factory.CreateBrowser();
        using var response = await browser.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, factory.OutboundRequests);
    }

    /// <summary>Attacker-controlled return URLs never redirect outside the application.</summary>
    [Theory]
    [InlineData("https://attacker.invalid/")]
    [InlineData("//attacker.invalid/")]
    [InlineData("/\\attacker.invalid/")]
    public async Task SignInRejectsExternalReturnUrls(string returnUrl)
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var factory = new SecurityWebApplicationFactory(scope) { TestRailResponder = _ => ValidUserResponse() };
        using var browser = factory.CreateBrowser();
        using var response = await SignInAsync(browser, returnUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    /// <summary>Logout changes state only on an authenticated POST with a fresh antiforgery token,
    /// and discards the server-side TestRail session so the cookie can no longer authenticate.</summary>
    [Fact]
    public async Task LogoutRequiresPostAndAntiforgery()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var factory = new SecurityWebApplicationFactory(scope) { TestRailResponder = _ => ValidUserResponse() };
        using var browser = factory.CreateBrowser();
        using var signIn = await SignInAsync(browser, "/");
        using var getLogout = await browser.GetAsync("/Logout");
        Assert.Equal(HttpStatusCode.Redirect, getLogout.StatusCode);
        using var withoutToken = await browser.PostAsync("/Logout", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
        // The TestRail connection is configured in this fixture, so /Setup is no longer reachable;
        // the logout form's antiforgery token is on every authenticated page via the shared layout.
        using var home = await browser.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        using var logout = await browser.PostAsync("/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(await home.Content.ReadAsStringAsync())
        }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        using var protectedPage = await browser.GetAsync("/Setup");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
    }

    /// <summary>Clearing the server-side credential store (e.g. simulating an app restart, since the
    /// store is in-memory only) revokes every already-issued cookie, because the session each one
    /// references no longer exists.</summary>
    [Fact]
    public async Task ClearingTheCredentialStoreRevokesExistingAuthentication()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var factory = new SecurityWebApplicationFactory(scope) { TestRailResponder = _ => ValidUserResponse() };
        using var browser = factory.CreateBrowser();
        // Target "/" rather than "/Setup": once the TestRail connection is configured, "/Setup" is
        // publicly routable-but-redirected (not authentication-gated), so it is no longer a valid
        // probe for cookie/session invalidation -- "/" remains behind the authenticated-user policy.
        using var signIn = await SignInAsync(browser, "/");
        factory.Services.GetRequiredService<TestRailCredentialStore>().Clear();

        using var response = await browser.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://localhost/Login", response.Headers.Location?.AbsoluteUri);
    }

    /// <summary>Repeated attempts are rate-limited and never leak the attempted credentials back to the caller.</summary>
    [Fact]
    public async Task RepeatedFailedSignInsAreRateLimited()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var factory = new SecurityWebApplicationFactory(scope) { TestRailResponder = _ => InvalidCredentialsResponse() };
        using var browser = factory.CreateBrowser();
        using var login = await browser.GetAsync("/Login");
        var token = Token(await login.Content.ReadAsStringAsync());
        for (var attempt = 0; attempt < 11; attempt++)
        {
            using var response = await browser.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Username"] = Username,
                ["Password"] = "incorrect-fixture-password",
                ["__RequestVerificationToken"] = token
            }));
            Assert.Equal(attempt < 10 ? HttpStatusCode.Unauthorized : HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        // The rate limiter rejects the 11th attempt before it reaches the handler, so only the
        // first 10 (each independently and safely rejected by TestRail) reach the connection.
        Assert.Equal(10, factory.OutboundRequests);
    }

    /// <summary>Cookie lifetimes are bounded and Azure DevOps redirects cannot forward requests.</summary>
    [Fact]
    public async Task AuthenticationAndHttpHandlersUseSecureDefaults()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var factory = new SecurityWebApplicationFactory(scope);
        using var browser = factory.CreateBrowser();
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        Assert.Equal(TimeSpan.FromMinutes(30), options.ExpireTimeSpan);

        var handler = factory.Services.GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(nameof(AzureDevOpsService));
        while (handler is DelegatingHandler delegating)
        {
            handler = Assert.IsAssignableFrom<HttpMessageHandler>(delegating.InnerHandler);
        }
        Assert.False(Assert.IsType<HttpClientHandler>(handler).AllowAutoRedirect);
    }

    /// <summary>Returns explicitly dummy credentials for the isolated application.</summary>
    private static TestRailSettings ConfiguredSettings() => new()
    {
        BaseUrl = "https://testrail.invalid",
        Username = "fixture@example.invalid",
        ApiKey = "fixture-only-token",
        ShowConsole = true
    };

    /// <summary>A canned TestRail success response for the signed-in fixture user.</summary>
    private static HttpResponseMessage ValidUserResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            """{"id":1,"name":"Fixture User","email":"fixture-user@example.test","role_id":1,"is_active":true,"is_admin":true}""",
            Encoding.UTF8, "application/json")
    };

    /// <summary>A canned TestRail rejection response for an invalid credential attempt.</summary>
    private static HttpResponseMessage InvalidCredentialsResponse() => new(HttpStatusCode.Unauthorized)
    {
        Content = new StringContent("""{"error":"Invalid or missing API key or password"}""", Encoding.UTF8, "application/json")
    };

    /// <summary>Submits a real Razor Pages antiforgery token with a login attempt.</summary>
    private static async Task<HttpResponseMessage> SignInAsync(
        HttpClient browser, string returnUrl, string password = Password)
    {
        using var login = await browser.GetAsync("/Login");
        var token = Token(await login.Content.ReadAsStringAsync());
        return await browser.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = Username,
            ["Password"] = password,
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token
        }));
    }

    /// <summary>Extracts a token from the rendered form rather than bypassing antiforgery validation.</summary>
    private static string Token(string html)
    {
        using var document = new HtmlParser().ParseDocument(html);
        return document.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value")
            ?? throw new InvalidOperationException("The response did not contain an antiforgery token.");
    }
}
