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
}
