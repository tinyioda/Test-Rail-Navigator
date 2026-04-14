namespace TestRailNavigator.Services;

/// <summary>
/// Configuration settings for connecting to TestRail.
/// </summary>
public class TestRailSettings
{
    /// <summary>
    /// Gets or sets the base URL of the TestRail instance.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the username (email) for authentication.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key for authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether write operations (create, edit, delete) are enabled.
    /// When false the application operates in read-only mode. Defaults to false.
    /// </summary>
    public bool AllowWrites { get; set; }

    /// <summary>
    /// Gets or sets the username required to access the Setup page.
    /// When empty the Setup page is unprotected.
    /// </summary>
    public string SetupUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password required to access the Setup page.
    /// When empty the Setup page is unprotected.
    /// </summary>
    public string SetupPassword { get; set; } = string.Empty;
}
