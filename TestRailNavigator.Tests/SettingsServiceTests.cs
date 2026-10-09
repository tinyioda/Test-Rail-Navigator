using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Unit coverage for settings persistence and configuration overlay behavior.</summary>
public class SettingsServiceTests
{
    /// <summary>No files and no configuration still yield a non-null default settings object.</summary>
    [Fact]
    public async Task GetSettingsAsync_ReturnsDefaultObjectWhenNoFilesOrConfigurationExist()
    {
        using var environment = new SettingsTestEnvironment();
        var service = new SettingsService(environment, new ConfigurationBuilder().Build());

        var settings = await service.GetSettingsAsync();

        Assert.NotNull(settings);
        Assert.Equal(string.Empty, settings.BaseUrl);
        Assert.Equal(string.Empty, settings.Username);
        Assert.Equal(string.Empty, settings.ApiKey);
    }

    /// <summary>Present configuration keys override legacy file values, including explicit empty strings.</summary>
    [Fact]
    public async Task GetSettingsAsync_UsesConfigurationOverridesWhenKeysArePresent()
    {
        using var environment = new SettingsTestEnvironment();
        await File.WriteAllTextAsync(
            Path.Combine(environment.RootPath, "testrail-settings.json"),
            JsonSerializer.Serialize(new TestRailSettings
            {
                BaseUrl = "https://legacy.example",
                Username = "legacy-user",
                ApiKey = "legacy-key"
            }));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestRail:BaseUrl"] = string.Empty,
                ["TestRail:Username"] = "config-user",
                ["TestRail:ApiKey"] = "config-key"
            })
            .Build();
        var service = new SettingsService(environment, configuration);

        var settings = await service.GetSettingsAsync();

        Assert.NotNull(settings);
        Assert.Equal(string.Empty, settings.BaseUrl);
        Assert.Equal("config-user", settings.Username);
        Assert.Equal("config-key", settings.ApiKey);
    }

    /// <summary>When the TestRail configuration section is absent entirely, legacy file values survive unchanged.</summary>
    [Fact]
    public async Task GetSettingsAsync_PreservesLegacyConnectionValuesWhenConfigurationSectionIsAbsent()
    {
        using var environment = new SettingsTestEnvironment();
        await File.WriteAllTextAsync(
            Path.Combine(environment.RootPath, "testrail-settings.json"),
            JsonSerializer.Serialize(new TestRailSettings
            {
                BaseUrl = "https://legacy.example",
                Username = "legacy-user",
                ApiKey = "legacy-key"
            }));

        var service = new SettingsService(environment, new ConfigurationBuilder().Build());

        var settings = await service.GetSettingsAsync();

        Assert.NotNull(settings);
        Assert.Equal("https://legacy.example", settings.BaseUrl);
        Assert.Equal("legacy-user", settings.Username);
        Assert.Equal("legacy-key", settings.ApiKey);
    }

    /// <summary>Saving writes connection values into appsettings.json while preserving unrelated JSON and leaving only non-connection fields in the legacy file.</summary>
    [Fact]
    public async Task SaveSettingsAsync_WritesConnectionToAppSettingsAndOtherFieldsToLegacyFile()
    {
        using var environment = new SettingsTestEnvironment();
        await File.WriteAllTextAsync(
            Path.Combine(environment.RootPath, "appsettings.json"),
            """
            {
              "Logging": {
                "LogLevel": {
                  "Default": "Information"
                }
              },
              "AllowedHosts": "*"
            }
            """);

        var service = new SettingsService(environment, new ConfigurationBuilder().Build());
        var settings = new TestRailSettings
        {
            BaseUrl = "https://saved.example",
            Username = "saved-user",
            ApiKey = "saved-key",
            AllowWrites = false
        };

        await service.SaveSettingsAsync(settings);

        var appSettingsJson = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(environment.RootPath, "appsettings.json")))!.AsObject();
        Assert.Equal("Information", appSettingsJson["Logging"]?["LogLevel"]?["Default"]?.GetValue<string>());
        Assert.Equal("*", appSettingsJson["AllowedHosts"]?.GetValue<string>());
        Assert.Equal("https://saved.example", appSettingsJson["TestRail"]?["BaseUrl"]?.GetValue<string>());
        Assert.Equal("saved-user", appSettingsJson["TestRail"]?["Username"]?.GetValue<string>());
        Assert.Equal("saved-key", appSettingsJson["TestRail"]?["ApiKey"]?.GetValue<string>());

        var legacyJson = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(environment.RootPath, "testrail-settings.json")))!.AsObject();
        Assert.False(legacyJson["AllowWrites"]?.GetValue<bool>() ?? true);
        Assert.False(legacyJson.ContainsKey("BaseUrl"));
        Assert.False(legacyJson.ContainsKey("Username"));
        Assert.False(legacyJson.ContainsKey("ApiKey"));
    }

    /// <summary>An explicit empty configuration override can make an otherwise populated installation read as unconfigured.</summary>
    [Fact]
    public async Task IsConfiguredAsync_TreatsPresentButEmptyConfigurationValuesAsOverrides()
    {
        using var environment = new SettingsTestEnvironment();
        await File.WriteAllTextAsync(
            Path.Combine(environment.RootPath, "testrail-settings.json"),
            JsonSerializer.Serialize(new TestRailSettings
            {
                BaseUrl = "https://legacy.example",
                Username = "legacy-user",
                ApiKey = "legacy-key"
            }));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestRail:BaseUrl"] = string.Empty,
                ["TestRail:Username"] = "config-user",
                ["TestRail:ApiKey"] = "config-key"
            })
            .Build();
        var service = new SettingsService(environment, configuration);

        Assert.False(await service.IsConfiguredAsync());
    }

    /// <summary>Minimal host-environment implementation for settings-service unit coverage.</summary>
    private sealed class SettingsTestEnvironment : IWebHostEnvironment, IDisposable
    {
        /// <summary>Initializes a unique test content root under the test output directory.</summary>
        public SettingsTestEnvironment()
        {
            RootPath = Path.Combine(AppContext.BaseDirectory, "settings-service-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        /// <summary>Gets the isolated content root.</summary>
        public string RootPath { get; }

        /// <inheritdoc />
        public string ApplicationName { get; set; } = "TestRailNavigator";

        /// <inheritdoc />
        public string EnvironmentName { get; set; } = "Production";

        /// <inheritdoc />
        public string ContentRootPath { get => RootPath; set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        /// <inheritdoc />
        public string WebRootPath { get; set; } = string.Empty;

        /// <inheritdoc />
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        /// <inheritdoc />
        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
