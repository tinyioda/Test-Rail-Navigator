using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Request model for updating an existing test case via the TestRail update_case endpoint.
/// </summary>
public class UpdateTestCaseRequest
{
    /// <summary>
    /// Gets or sets the title of the test case.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the type identifier of the test case.
    /// </summary>
    [JsonPropertyName("type_id")]
    public int? TypeId { get; set; }

    /// <summary>
    /// Gets or sets the priority identifier of the test case.
    /// </summary>
    [JsonPropertyName("priority_id")]
    public int? PriorityId { get; set; }

    /// <summary>
    /// Gets or sets the estimated time for the test case (e.g. "30s", "1m 45s").
    /// </summary>
    [JsonPropertyName("estimate")]
    public string? Estimate { get; set; }

    /// <summary>
    /// Gets or sets the milestone identifier the test case belongs to.
    /// </summary>
    [JsonPropertyName("milestone_id")]
    public int? MilestoneId { get; set; }

    /// <summary>
    /// Gets or sets the external references (e.g. JIRA ticket IDs).
    /// </summary>
    [JsonPropertyName("refs")]
    public string? Refs { get; set; }

    /// <summary>
    /// Gets or sets the preconditions for the test case.
    /// </summary>
    [JsonPropertyName("custom_preconds")]
    public string? Preconditions { get; set; }

    /// <summary>
    /// Gets or sets the test steps description.
    /// </summary>
    [JsonPropertyName("custom_steps")]
    public string? Steps { get; set; }

    /// <summary>
    /// Gets or sets the expected result description.
    /// </summary>
    [JsonPropertyName("custom_expected")]
    public string? ExpectedResult { get; set; }
}
