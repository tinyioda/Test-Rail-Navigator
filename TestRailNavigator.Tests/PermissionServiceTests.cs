using System.Net;
using System.Text;
using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Unit coverage for permission loading, fallback behavior, and cache invalidation.</summary>
public class PermissionServiceTests
{
    /// <summary>The first successful load is cached and reused by later calls.</summary>
    [Fact]
    public async Task GetPermissionsAsync_CachesTheFirstSuccessfulResult()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var handler = new RecordingHandler(_ => JsonResponse("""
            {"id":1,"name":"Alice","email":"alice@example.test","role_id":4,"is_active":true,"is_admin":false}
            """));
        using var client = new HttpClient(handler);

        var service = new PermissionService(new TestRailClient(client, scope.Settings), new ConsoleLogService());

        var first = await service.GetPermissionsAsync();
        var second = await service.GetPermissionsAsync();

        Assert.Same(first, second);
        Assert.Equal(1, handler.RequestCount);
        Assert.True(first.CanManageRuns);
        Assert.False(first.IsAdmin);
        Assert.Equal("Lead", first.RoleName);
    }

    /// <summary>An unresolved current user falls back to the read-only defaults instead of throwing.</summary>
    [Fact]
    public async Task GetPermissionsAsync_ReturnsReadOnlyWhenCurrentUserIsMissing()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var handler = new RecordingHandler(_ => JsonResponse("null"));
        using var client = new HttpClient(handler);
        var log = new ConsoleLogService();
        var service = new PermissionService(new TestRailClient(client, scope.Settings), log);

        var permissions = await service.GetPermissionsAsync();

        Assert.Equal("Unknown", permissions.UserName);
        Assert.False(permissions.CanAddResults);
        Assert.Contains(log.GetMessages(), message => message.Contains("defaulting to read-only permissions", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Transport or API failures also fall back to read-only permissions and are logged.</summary>
    [Fact]
    public async Task GetPermissionsAsync_ReturnsReadOnlyWhenTheClientThrows()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"error":"boom"}""", Encoding.UTF8, "application/json")
        });
        using var client = new HttpClient(handler);
        var log = new ConsoleLogService();
        var service = new PermissionService(new TestRailClient(client, scope.Settings), log);

        var permissions = await service.GetPermissionsAsync();

        Assert.Equal("Unknown", permissions.UserName);
        Assert.False(permissions.CanManageCases);
        Assert.Contains(log.GetMessages(), message => message.Contains("boom", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Clearing the cache forces the next call to re-fetch the current user.</summary>
    [Fact]
    public async Task ClearCache_ForcesAReloadOnTheNextCall()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var handler = new RecordingHandler(request =>
        {
            return request switch
            {
                _ when request.RequestCount == 1 => JsonResponse("""
                    {"id":1,"name":"Alice","email":"alice@example.test","role_id":2,"is_active":true,"is_admin":false}
                    """),
                _ => JsonResponse("""
                    {"id":2,"name":"Bob","email":"bob@example.test","role_id":5,"is_active":true,"is_admin":false}
                    """)
            };
        });
        using var client = new HttpClient(handler);
        var service = new PermissionService(new TestRailClient(client, scope.Settings), new ConsoleLogService());

        var first = await service.GetPermissionsAsync();
        service.ClearCache();
        var second = await service.GetPermissionsAsync();

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal("Alice", first.UserName);
        Assert.Equal("Bob", second.UserName);
        Assert.True(second.IsAdmin);
    }

    /// <summary>Creates a minimally configured settings object for TestRail-client unit coverage.</summary>
    private static TestRailSettings ConfiguredSettings() => new()
    {
        BaseUrl = "https://testrail.example.test",
        Username = "fixture-user",
        ApiKey = "fixture-key"
    };

    /// <summary>Creates a JSON response payload for the fake HTTP handler.</summary>
    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    /// <summary>Captures requests and returns caller-defined fake responses.</summary>
    private sealed class RecordingHandler(Func<ObservedRequest, HttpResponseMessage> responder) : HttpMessageHandler
    {
        /// <summary>Gets the number of requests handled so far.</summary>
        public int RequestCount { get; private set; }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responder(new ObservedRequest(request, RequestCount)));
        }
    }

    /// <summary>Describes one intercepted request.</summary>
    private sealed class ObservedRequest(HttpRequestMessage request, int requestCount)
    {
        /// <summary>Gets the original HTTP request.</summary>
        public HttpRequestMessage Request { get; } = request;

        /// <summary>Gets the 1-based ordinal for this request.</summary>
        public int RequestCount { get; } = requestCount;
    }
}
