using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Runs the real request pipeline with isolated credentials, keys, data, and blocked outbound HTTP.</summary>
internal sealed class SecurityWebApplicationFactory(
    TestSettingsScope scope,
    IReadOnlyDictionary<string, string?>? configuration = null) : WebApplicationFactory<Program>
{
    private int _outboundRequests;

    /// <summary>Gets the number of unexpected outbound integration requests.</summary>
    public int OutboundRequests => _outboundRequests;

    /// <summary>
    /// Optional canned-response function for TestRail requests, used to simulate a successful or
    /// failed live credential validation during sign-in. Left null (the default) for scenarios that
    /// must never actually reach TestRail, in which case any TestRail request fails the test.
    /// </summary>
    public Func<HttpRequestMessage, HttpResponseMessage?>? TestRailResponder { get; set; }

    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var settings = new Dictionary<string, string?>
        {
            ["DataProtection:KeysPath"] = Path.Combine(scope.RootPath, "keys"),
            ["Logging:LogLevel:Default"] = "Warning"
        };
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(settings));
        return base.CreateHost(builder);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseContentRoot(scope.RootPath);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            if (configuration is not null)
            {
                config.AddInMemoryCollection(configuration);
            }
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<SettingsService>();
            services.AddSingleton(scope.Settings);
            services.AddHttpClient<TestRailClient>()
                .AddHttpMessageHandler(() => new FakeTestRailHandler(
                    request => TestRailResponder?.Invoke(request),
                    () => Interlocked.Increment(ref _outboundRequests)));
        });
    }

    /// <summary>Creates a cookie-capable client without hiding authentication redirects.</summary>
    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });
}

