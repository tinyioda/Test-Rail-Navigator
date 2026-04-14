using System.Text.Json;

namespace TestRailNavigator.Services;

/// <summary>
/// Service for managing TestRail settings persistence.
/// </summary>
public class SettingsService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TestRailSettings? _cachedSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    /// <param name="environment">The web host environment.</param>
    public SettingsService(IWebHostEnvironment environment)
    {
        _settingsPath = Path.Combine(environment.ContentRootPath, "testrail-settings.json");
    }

    /// <summary>
    /// Gets the current TestRail settings.
    /// </summary>
    /// <returns>The settings, or null if not configured.</returns>
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

            if (!File.Exists(_settingsPath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_settingsPath);
            _cachedSettings = JsonSerializer.Deserialize<TestRailSettings>(json, _jsonOptions);
            return _cachedSettings;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Saves the TestRail settings.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    public async Task SaveSettingsAsync(TestRailSettings settings)
    {
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(_settingsPath, json);
            _cachedSettings = settings;
        }
        finally
        {
            _lock.Release();
        }
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
