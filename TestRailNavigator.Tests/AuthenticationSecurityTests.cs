using System.Net;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Covers the single-admin boundary through the actual Razor Pages request pipeline.</summary>
public class AuthenticationSecurityTests
{
    private const string Username = "fixture-admin";
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
            "/GenerateCases?handler=Enrich", "/GenerateHierarchy",
            "/CreatePlanFromStory?handler=Confirm", "/Setup", "/Setup?handler=Login",
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

        Assert.Equal(0, factory.OutboundRequests);
    }

    /// <summary>Missing credentials cannot reopen public Setup or issue an administrator cookie.</summary>
    [Theory]
    [InlineData("", "")]
    [InlineData("admin", "")]
    [InlineData("", "password")]
    public async Task MissingCredentialsFailClosed(string username, string password)
    {
        using var scope = await TestSettingsScope.CreateAsync(new TestRailSettings
        {
            SetupUsername = username,
            SetupPassword = password
        });
        using var factory = new SecurityWebApplicationFactory(scope);
        using var browser = factory.CreateBrowser();

        using var login = await browser.GetAsync("/Login");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
        Assert.Contains("Administrator credentials are not configured.", await login.Content.ReadAsStringAsync());
        using var setup = await browser.GetAsync("/Setup");
        Assert.Equal(HttpStatusCode.Redirect, setup.StatusCode);

        var auth = factory.Services.GetRequiredService<AdminAuthenticationService>();
        Assert.False(await auth.IsConfiguredAsync());
        Assert.Null(await auth.AuthenticateAsync(username, password));
        Assert.Equal(0, factory.OutboundRequests);
    }

    /// <summary>Deployment credentials allow the first administrator to provision an empty installation.</summary>
    [Fact]
    public async Task DeploymentCredentialsSupportFirstRunWithoutSettingsFile()
    {
        using var scope = await TestSettingsScope.CreateAsync(new TestRailSettings());
        File.Delete(Path.Combine(scope.RootPath, "testrail-settings.json"));
        using var factory = new SecurityWebApplicationFactory(scope, new Dictionary<string, string?>
        {
            ["TestRail:SetupUsername"] = Username,
            ["TestRail:SetupPassword"] = Password
        });
        using var browser = factory.CreateBrowser();
        using var response = await SignInAsync(browser, "/Setup");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Setup", response.Headers.Location?.OriginalString);
        using var setup = await browser.GetAsync("/Setup");
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        Assert.Equal(0, factory.OutboundRequests);
    }

    /// <summary>Sign-in is antiforgery-protected, generic on failure, and uses secure administrator cookies.</summary>
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

        using var invalid = await SignInAsync(browser, "/Setup", password: "incorrect-fixture-password");
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Contains("Invalid username or password.", await invalid.Content.ReadAsStringAsync());
        Assert.DoesNotContain("incorrect-fixture-password", await invalid.Content.ReadAsStringAsync());

        using var valid = await SignInAsync(browser, "/Setup");
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);
        var cookie = Assert.Single(valid.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("TestRailNavigator.Admin=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        using var setup = await browser.GetAsync("/Setup");
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        Assert.True(setup.Headers.CacheControl?.NoStore);
        Assert.Contains("Sign out", await setup.Content.ReadAsStringAsync());
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
        using var factory = new SecurityWebApplicationFactory(scope);
        using var browser = factory.CreateBrowser();
        using var response = await SignInAsync(browser, returnUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    /// <summary>Logout changes state only on an authenticated POST with a fresh antiforgery token.</summary>
    [Fact]
    public async Task LogoutRequiresPostAndAntiforgery()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var factory = new SecurityWebApplicationFactory(scope);
        using var browser = factory.CreateBrowser();
        using var signIn = await SignInAsync(browser, "/Setup");
        using var getLogout = await browser.GetAsync("/Logout");
        Assert.Equal(HttpStatusCode.Redirect, getLogout.StatusCode);
        using var withoutToken = await browser.PostAsync("/Logout", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
        using var setup = await browser.GetAsync("/Setup");
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        using var logout = await browser.PostAsync("/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(await setup.Content.ReadAsStringAsync())
        }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        using var protectedPage = await browser.GetAsync("/Setup");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
    }

    /// <summary>Credential changes invalidate already-issued protected cookies.</summary>
    [Fact]
    public async Task CredentialRotationRevokesExistingAuthentication()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var factory = new SecurityWebApplicationFactory(scope);
        using var browser = factory.CreateBrowser();
        using var signIn = await SignInAsync(browser, "/Setup");
        var changed = ConfiguredSettings();
        changed.SetupPassword = "changed-fixture-password";
        await scope.Settings.SaveSettingsAsync(changed);

        using var response = await browser.GetAsync("/Setup");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://localhost/Login", response.Headers.Location?.AbsoluteUri);
    }

    /// <summary>Repeated attempts are limited without leaking credentials or contacting TestRail.</summary>
    [Fact]
    public async Task RepeatedFailedSignInsAreRateLimited()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var factory = new SecurityWebApplicationFactory(scope);
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
        Assert.Equal(0, factory.OutboundRequests);
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
        SetupUsername = Username,
        SetupPassword = Password,
        ShowConsole = true
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
