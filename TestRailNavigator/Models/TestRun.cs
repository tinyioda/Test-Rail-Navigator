using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Represents a TestRail test run.
/// </summary>
public class TestRun
{
    /// <summary>
    /// Gets or sets the unique identifier of the test run.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the project identifier this run belongs to.
    /// </summary>
    [JsonPropertyName("project_id")]
    public int ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the milestone identifier associated with this run.
    /// </summary>
    [JsonPropertyName("milestone_id")]
    public int? MilestoneId { get; set; }

    /// <summary>
    /// Gets or sets the test plan identifier this run belongs to, if any.
    /// </summary>
    [JsonPropertyName("plan_id")]
    public int? PlanId { get; set; }

    /// <summary>
    /// Gets or sets the suite identifier this run is based on.
    /// </summary>
    [JsonPropertyName("suite_id")]
    public int? SuiteId { get; set; }

    /// <summary>
    /// Gets or sets the name of the test run.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the test run.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the count of passed tests in the run.
    /// </summary>
    [JsonPropertyName("passed_count")]
    public int PassedCount { get; set; }

    /// <summary>
    /// Gets or sets the count of failed tests in the run.
    /// </summary>
    [JsonPropertyName("failed_count")]
    public int FailedCount { get; set; }

    /// <summary>
    /// Gets or sets the count of blocked tests in the run.
    /// </summary>
    [JsonPropertyName("blocked_count")]
    public int BlockedCount { get; set; }

    /// <summary>
    /// Gets or sets the count of untested tests in the run.
    /// </summary>
    [JsonPropertyName("untested_count")]
    public int UntestedCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the test run is completed.
    /// </summary>
    [JsonPropertyName("is_completed")]
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Gets or sets the URL to the test run in TestRail.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
