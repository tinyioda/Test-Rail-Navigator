using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Represents a TestRail milestone.
/// </summary>
public class Milestone
{
    /// <summary>
    /// Gets or sets the unique identifier of the milestone.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the milestone.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the milestone.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the project identifier this milestone belongs to.
    /// </summary>
    [JsonPropertyName("project_id")]
    public int ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the due date as a Unix timestamp.
    /// </summary>
    [JsonPropertyName("due_on")]
    public long? DueOn { get; set; }

    /// <summary>
    /// Gets or sets the start date as a Unix timestamp.
    /// </summary>
    [JsonPropertyName("start_on")]
    public long? StartOn { get; set; }

    /// <summary>
    /// Gets or sets the completed date as a Unix timestamp.
    /// </summary>
    [JsonPropertyName("completed_on")]
    public long? CompletedOn { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the milestone is completed.
    /// </summary>
    [JsonPropertyName("is_completed")]
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the milestone has started.
    /// </summary>
    [JsonPropertyName("is_started")]
    public bool IsStarted { get; set; }

    /// <summary>
    /// Gets or sets the parent milestone identifier for sub-milestones.
    /// </summary>
    [JsonPropertyName("parent_id")]
    public int? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the URL to the milestone in TestRail.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
