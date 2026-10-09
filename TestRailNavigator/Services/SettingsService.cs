using System.Text.Json;
using System.Text.Json.Nodes;

namespace TestRailNavigator.Services;

/// <summary>
/// Service for managing TestRail settings persistence.
/// </summary>
public class SettingsService
{
    private readonly string _settingsPath;
    private readonly string _appSettingsPath;
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TestRailSettings? _cachedSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    /// <param name="environment">The web host environment.</param>
    /// <param name="configuration">The application configuration (used for the TestRail connection section in appsettings.json).</param>
    public SettingsService(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _settingsPath = Path.Combine(environment.ContentRootPath, "testrail-settings.json");
        _appSettingsPath = Path.Combine(environment.ContentRootPath, "appsettings.json");
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the current TestRail settings. The connection fields (<see cref="TestRailSettings.BaseUrl"/>,
    /// <see cref="TestRailSettings.Username"/>, <see cref="TestRailSettings.ApiKey"/>) are sourced from the
    /// <c>TestRail</c> section of <c>appsettings.json</c> when present, overriding any value persisted in the
    /// legacy <c>testrail-settings.json</c> file.
    /// </summary>
    /// <returns>The settings. Never null; unconfigured fields are empty strings.</returns>
    public async Task<TestRailSettings?> GetSettingsAsync()
    {
        if (_cachedSettings is not null)
        {
            return _cachedSettings;
        }

        await _lock.WaitAsync();
        try
        {
            if (_cachedSettings is not null)
            {
                return _cachedSettings;
            }

            TestRailSettings? settings = null;
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                settings = JsonSerializer.Deserialize<TestRailSettings>(json, _jsonOptions);
            }
            settings ??= new TestRailSettings();

            ApplyConnectionFromConfiguration(settings);

#if DEBUG
            // In DEBUG builds (Visual Studio local runs), always allow writes regardless of
            // what is persisted on disk. Release/deployed builds honour the JSON value,
            // which defaults to false so production runs in read-only mode unless explicitly
            // enabled through the Setup page.
            settings.AllowWrites = true;
#endif
            _cachedSettings = settings;
            return _cachedSettings;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Overlays the <c>TestRail:BaseUrl</c>/<c>Username</c>/<c>ApiKey</c>, <c>AzureDevOps:BaseUrl</c>/<c>Pat</c>,
    /// and <c>Jira:BaseUrl</c>/<c>Email</c>/<c>ApiToken</c> configuration values, when present, onto the given
    /// settings instance.
    /// </summary>
    private void ApplyConnectionFromConfiguration(TestRailSettings settings)
    {
        var baseUrl = _configuration["TestRail:BaseUrl"];
        if (baseUrl is not null) settings.BaseUrl = baseUrl;

        var username = _configuration["TestRail:Username"];
        if (username is not null) settings.Username = username;

        var apiKey = _configuration["TestRail:ApiKey"];
        if (apiKey is not null) settings.ApiKey = apiKey;

        var azureDevOpsBaseUrl = _configuration["AzureDevOps:BaseUrl"];
        if (azureDevOpsBaseUrl is not null) settings.AzureDevOpsBaseUrl = azureDevOpsBaseUrl;

        var azureDevOpsPat = _configuration["AzureDevOps:Pat"];
        if (azureDevOpsPat is not null) settings.AzureDevOpsPat = azureDevOpsPat;

        var jiraBaseUrl = _configuration["Jira:BaseUrl"];
        if (jiraBaseUrl is not null) settings.JiraBaseUrl = jiraBaseUrl;

        var jiraEmail = _configuration["Jira:Email"];
        if (jiraEmail is not null) settings.JiraEmail = jiraEmail;

        var jiraApiToken = _configuration["Jira:ApiToken"];
        if (jiraApiToken is not null) settings.JiraApiToken = jiraApiToken;
    }

    /// <summary>
    /// Saves the TestRail settings. The connection fields (<see cref="TestRailSettings.BaseUrl"/>,
    /// <see cref="TestRailSettings.Username"/>, <see cref="TestRailSettings.ApiKey"/>), the Azure DevOps
    /// fields (<see cref="TestRailSettings.AzureDevOpsBaseUrl"/>, <see cref="TestRailSettings.AzureDevOpsPat"/>),
    /// and the Jira fields (<see cref="TestRailSettings.JiraBaseUrl"/>, <see cref="TestRailSettings.JiraEmail"/>,
    /// <see cref="TestRailSettings.JiraApiToken"/>) are written to <c>appsettings.json</c> at runtime; all other
    /// fields continue to persist to <c>testrail-settings.json</c>.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    public async Task SaveSettingsAsync(TestRailSettings settings)
    {
        await _lock.WaitAsync();
        try
        {
            await WriteConnectionToAppSettingsAsync(settings);

            var legacySettings = JsonSerializer.SerializeToNode(settings, _jsonOptions) as JsonObject ?? new JsonObject();
            legacySettings.Remove(nameof(TestRailSettings.BaseUrl));
            legacySettings.Remove(nameof(TestRailSettings.Username));
            legacySettings.Remove(nameof(TestRailSettings.ApiKey));
            legacySettings.Remove(nameof(TestRailSettings.AzureDevOpsBaseUrl));
            legacySettings.Remove(nameof(TestRailSettings.AzureDevOpsPat));
            legacySettings.Remove(nameof(TestRailSettings.JiraBaseUrl));
            legacySettings.Remove(nameof(TestRailSettings.JiraEmail));
            legacySettings.Remove(nameof(TestRailSettings.JiraApiToken));

            var json = legacySettings.ToJsonString(_jsonOptions);
            await File.WriteAllTextAsync(_settingsPath, json);
            _cachedSettings = settings;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Merges the TestRail, Azure DevOps, and Jira connection values into <c>appsettings.json</c> on disk,
    /// preserving every other section (Logging, AllowedHosts, etc.) untouched.
    /// </summary>
    private async Task WriteConnectionToAppSettingsAsync(TestRailSettings settings)
    {
        JsonNode root;
        if (File.Exists(_appSettingsPath))
        {
            var existingJson = await File.ReadAllTextAsync(_appSettingsPath);
            root = JsonNode.Parse(existingJson) ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        if (root is not JsonObject rootObject)
        {
            throw new InvalidOperationException("appsettings.json root must be a JSON object.");
        }

        if (rootObject["TestRail"] is not JsonObject testRailSection)
        {
            testRailSection = new JsonObject();
            rootObject["TestRail"] = testRailSection;
        }

        testRailSection["BaseUrl"] = settings.BaseUrl;
        testRailSection["Username"] = settings.Username;
        testRailSection["ApiKey"] = settings.ApiKey;

        if (rootObject["AzureDevOps"] is not JsonObject azureDevOpsSection)
        {
            azureDevOpsSection = new JsonObject();
            rootObject["AzureDevOps"] = azureDevOpsSection;
        }

        azureDevOpsSection["BaseUrl"] = settings.AzureDevOpsBaseUrl;
        azureDevOpsSection["Pat"] = settings.AzureDevOpsPat;

        if (rootObject["Jira"] is not JsonObject jiraSection)
        {
            jiraSection = new JsonObject();
            rootObject["Jira"] = jiraSection;
        }

        jiraSection["BaseUrl"] = settings.JiraBaseUrl;
        jiraSection["Email"] = settings.JiraEmail;
        jiraSection["ApiToken"] = settings.JiraApiToken;

        var updatedJson = rootObject.ToJsonString(_jsonOptions);
        await File.WriteAllTextAsync(_appSettingsPath, updatedJson);
    }

    /// <summary>
    /// Checks if TestRail settings are configured.
    /// </summary>
    /// <returns>True if settings exist and are valid.</returns>
    public async Task<bool> IsConfiguredAsync()
    {
        var settings = await GetSettingsAsync();
        return settings is not null
            && !string.IsNullOrWhiteSpace(settings.BaseUrl)
            && !string.IsNullOrWhiteSpace(settings.Username)
            && !string.IsNullOrWhiteSpace(settings.ApiKey);
    }

    /// <summary>
    /// Checks if write operations are enabled in the current settings.
    /// </summary>
    /// <returns>True if writes are allowed; false otherwise.</returns>
    public async Task<bool> AreWritesEnabledAsync()
    {
        var settings = await GetSettingsAsync();
        return settings?.AllowWrites == true;
    }
}
