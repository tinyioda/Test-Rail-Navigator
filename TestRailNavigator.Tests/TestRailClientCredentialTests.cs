using System.Net;
using System.Text;
using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Unit coverage for how <see cref="TestRailClient"/> resolves which credentials to send.</summary>
public class TestRailClientCredentialTests
{
    /// <summary>With no per-request override, the configured service-account credentials are used.</summary>
    [Fact]
    public async Task UsesConfiguredServiceAccountWhenNoOverrideIsPresent()
    {
        using var scope = await TestSettingsScope.CreateAsync(new TestRailSettings
        {
            BaseUrl = "https://testrail.invalid",
            Username = "service-account@example.invalid",
            ApiKey = "service-account-token"
        });
        using var handler = new RecordingHandler(_ => JsonResponse("""{"id":1,"name":"Service Account"}"""));
        using var client = new HttpClient(handler);
        var testRailClient = new TestRailClient(client, scope.Settings, new CurrentTestRailCredentials());

        await testRailClient.GetCurrentUserAsync();

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(BasicAuthHeader("service-account@example.invalid", "service-account-token"), handler.LastAuthorization);
    }

    /// <summary>A per-request override (the signed-in user's own credentials) takes priority over the
    /// configured service account, so TestRail enforces that user's own role and permissions.</summary>
    [Fact]
    public async Task PrefersTheCurrentRequestsOverrideCredentialsOverTheConfiguredServiceAccount()
    {
        using var scope = await TestSettingsScope.CreateAsync(new TestRailSettings
        {
            BaseUrl = "https://testrail.invalid",
            Username = "service-account@example.invalid",
            ApiKey = "service-account-token"
        });
        using var handler = new RecordingHandler(_ => JsonResponse("""{"id":2,"name":"Signed-In User"}"""));
        using var client = new HttpClient(handler);
        var currentCredentials = new CurrentTestRailCredentials
        {
            Username = "signed-in-user@example.invalid",
            Secret = "signed-in-user-password"
        };
        var testRailClient = new TestRailClient(client, scope.Settings, currentCredentials);

        await testRailClient.GetCurrentUserAsync();

        Assert.Equal(BasicAuthHeader("signed-in-user@example.invalid", "signed-in-user-password"), handler.LastAuthorization);
    }

    /// <summary>Blank override fields (the default, unauthenticated state) fall back to the service account
    /// rather than sending an empty Basic-auth header.</summary>
    [Fact]
    public async Task FallsBackToServiceAccountWhenOverrideFieldsAreBlank()
    {
        using var scope = await TestSettingsScope.CreateAsync(new TestRailSettings
        {
            BaseUrl = "https://testrail.invalid",
            Username = "service-account@example.invalid",
            ApiKey = "service-account-token"
        });
        using var handler = new RecordingHandler(_ => JsonResponse("""{"id":1,"name":"Service Account"}"""));
        using var client = new HttpClient(handler);
        var testRailClient = new TestRailClient(client, scope.Settings, new CurrentTestRailCredentials { Username = "", Secret = "" });

        await testRailClient.GetCurrentUserAsync();

        Assert.Equal(BasicAuthHeader("service-account@example.invalid", "service-account-token"), handler.LastAuthorization);
    }

    private static string BasicAuthHeader(string username, string secret) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{secret}"));

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        /// <summary>Gets the number of requests handled so far.</summary>
        public int RequestCount { get; private set; }

        /// <summary>Gets the Authorization header value from the most recent request, if any.</summary>
        public string? LastAuthorization { get; private set; }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastAuthorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(responder(request));
        }
    }
}
