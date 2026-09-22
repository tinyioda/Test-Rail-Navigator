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
            var settings = new Dictionary<string, string?>
            {
                ["TestRail:SetupUsername"] = null,
                ["TestRail:SetupPassword"] = null
            };
            foreach (var (key, value) in configuration ?? new Dictionary<string, string?>())
            {
                settings[key] = value;
            }
            config.AddInMemoryCollection(settings);
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<SettingsService>();
            services.AddSingleton(scope.Settings);
            services.AddHttpClient<TestRailClient>()
                .AddHttpMessageHandler(CreateGuard);
            services.AddHttpClient<AzureDevOpsService>()
                .AddHttpMessageHandler(CreateGuard);
        });
    }

    /// <summary>Creates a cookie-capable client without hiding authentication redirects.</summary>
    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    /// <summary>Prevents a regression from sending even dummy integration credentials to a network.</summary>
    private RejectExternalRequestsHandler CreateGuard() =>
        new(() => Interlocked.Increment(ref _outboundRequests));
}
