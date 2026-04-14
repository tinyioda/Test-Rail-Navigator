using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Request model for creating a new TestRail project.
/// </summary>
public class CreateProjectRequest
{
    /// <summary>
    /// Gets or sets the name of the project (required).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the announcement/description of the project.
    /// </summary>
    [JsonPropertyName("announcement")]
    public string? Announcement { get; set; }

    /// <summary>
    /// Gets or sets whether to show the announcement.
    /// </summary>
    [JsonPropertyName("show_announcement")]
    public bool ShowAnnouncement { get; set; }

    /// <summary>
    /// Gets or sets the suite mode (1 = single, 2 = single + baselines, 3 = multiple suites).
    /// </summary>
    [JsonPropertyName("suite_mode")]
    public int SuiteMode { get; set; } = 1;
}
