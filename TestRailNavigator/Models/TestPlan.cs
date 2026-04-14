using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Represents a TestRail test plan (a container that groups multiple test runs).
/// </summary>
public class TestPlan
{
    /// <summary>
    /// Gets or sets the unique identifier of the test plan.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the test plan.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the test plan.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the project identifier this plan belongs to.
    /// </summary>
    [JsonPropertyName("project_id")]
    public int ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the milestone identifier associated with this plan.
    /// </summary>
    [JsonPropertyName("milestone_id")]
    public int? MilestoneId { get; set; }

    /// <summary>
    /// Gets or sets the count of passed tests across all runs in the plan.
    /// </summary>
    [JsonPropertyName("passed_count")]
    public int PassedCount { get; set; }

    /// <summary>
    /// Gets or sets the count of failed tests across all runs in the plan.
    /// </summary>
    [JsonPropertyName("failed_count")]
    public int FailedCount { get; set; }

    /// <summary>
    /// Gets or sets the count of blocked tests across all runs in the plan.
    /// </summary>
    [JsonPropertyName("blocked_count")]
    public int BlockedCount { get; set; }

    /// <summary>
    /// Gets or sets the count of untested tests across all runs in the plan.
    /// </summary>
    [JsonPropertyName("untested_count")]
    public int UntestedCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the test plan is completed.
    /// </summary>
    [JsonPropertyName("is_completed")]
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp as a Unix epoch.
    /// </summary>
    [JsonPropertyName("created_on")]
    public long? CreatedOn { get; set; }

    /// <summary>
    /// Gets or sets the plan entries (each entry maps to a suite and contains runs).
    /// </summary>
    /// <remarks>
    /// Only populated when fetching a single plan via get_plan. The get_plans list endpoint does not include entries.
    /// </remarks>
    [JsonPropertyName("entries")]
    public List<TestPlanEntry>? Entries { get; set; }

    /// <summary>
    /// Gets or sets the URL to the test plan in TestRail.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
