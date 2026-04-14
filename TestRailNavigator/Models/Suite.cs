using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Represents a TestRail test suite (a container for test cases).
/// </summary>
public class Suite
{
    /// <summary>
    /// Gets or sets the unique identifier of the suite.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the suite.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the suite.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the project identifier this suite belongs to.
    /// </summary>
    [JsonPropertyName("project_id")]
    public int ProjectId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the master suite.
    /// </summary>
    [JsonPropertyName("is_master")]
    public bool IsMaster { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a baseline suite.
    /// </summary>
    [JsonPropertyName("is_baseline")]
    public bool IsBaseline { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the suite is completed.
    /// </summary>
    [JsonPropertyName("is_completed")]
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Gets or sets the URL to the suite in TestRail.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
