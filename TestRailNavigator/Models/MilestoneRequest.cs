using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Request model for creating or updating a TestRail milestone.
/// </summary>
public class MilestoneRequest
{
    /// <summary>
    /// Gets or sets the name of the milestone (required for creation).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the milestone.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the start date as a Unix timestamp.
    /// </summary>
    [JsonPropertyName("start_on")]
    public long? StartOn { get; set; }

    /// <summary>
    /// Gets or sets the due date as a Unix timestamp.
    /// </summary>
    [JsonPropertyName("due_on")]
    public long? DueOn { get; set; }

    /// <summary>
    /// Gets or sets the parent milestone identifier for sub-milestones.
    /// </summary>
    [JsonPropertyName("parent_id")]
    public int? ParentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the milestone is completed.
    /// Used only for updates.
    /// </summary>
    [JsonPropertyName("is_completed")]
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the milestone has started.
    /// Used only for updates.
    /// </summary>
    [JsonPropertyName("is_started")]
    public bool IsStarted { get; set; }
}
