using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Represents a TestRail project.
/// </summary>
public class Project
{
    /// <summary>
    /// Gets or sets the unique identifier of the project.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the project.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project announcement text.
    /// </summary>
    [JsonPropertyName("announcement")]
    public string? Announcement { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the project is completed.
    /// </summary>
    [JsonPropertyName("is_completed")]
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Gets or sets the suite mode of the project.
    /// 1 = single suite, 2 = single suite + baselines, 3 = multiple suites.
    /// </summary>
    [JsonPropertyName("suite_mode")]
    public int SuiteMode { get; set; }

    /// <summary>
    /// Gets or sets the URL to the project in TestRail.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
