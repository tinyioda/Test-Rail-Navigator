using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Represents a test instance within a test run.
/// This is a run-time instance linked to a <see cref="TestCase"/> from the case library.
/// </summary>
public class Test
{
    /// <summary>
    /// Gets or sets the unique identifier of the test.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the permanent test case identifier this test is an instance of.
    /// </summary>
    [JsonPropertyName("case_id")]
    public int CaseId { get; set; }

    /// <summary>
    /// Gets or sets the title of the test.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status identifier of the test.
    /// </summary>
    [JsonPropertyName("status_id")]
    public int? StatusId { get; set; }

    /// <summary>
    /// Gets or sets the run identifier this test belongs to.
    /// </summary>
    [JsonPropertyName("run_id")]
    public int RunId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user this test is assigned to.
    /// </summary>
    [JsonPropertyName("assignedto_id")]
    public int? AssignedToId { get; set; }

    /// <summary>
    /// Gets or sets the type identifier of the underlying test case.
    /// </summary>
    [JsonPropertyName("type_id")]
    public int? TypeId { get; set; }

    /// <summary>
    /// Gets or sets the priority identifier of the underlying test case.
    /// </summary>
    [JsonPropertyName("priority_id")]
    public int? PriorityId { get; set; }

    /// <summary>
    /// Gets or sets the estimated time for the test (e.g. "30s", "1m 45s").
    /// </summary>
    [JsonPropertyName("estimate")]
    public string? Estimate { get; set; }

    /// <summary>
    /// Gets or sets the references for the underlying test case.
    /// </summary>
    [JsonPropertyName("refs")]
    public string? Refs { get; set; }

    /// <summary>
    /// Gets or sets the preconditions (custom field).
    /// </summary>
    [JsonPropertyName("custom_preconds")]
    public string? Preconditions { get; set; }

    /// <summary>
    /// Gets or sets the steps (custom field).
    /// </summary>
    [JsonPropertyName("custom_steps")]
    public string? Steps { get; set; }

    /// <summary>
    /// Gets or sets the expected result (custom field).
    /// </summary>
    [JsonPropertyName("custom_expected")]
    public string? ExpectedResult { get; set; }

    /// <summary>
    /// Gets the human-readable status name based on the status identifier.
    /// </summary>
    public string StatusName => StatusId switch
    {
        1 => "Passed",
        2 => "Blocked",
        3 => "Untested",
        4 => "Retest",
        5 => "Failed",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the human-readable priority name based on the priority identifier.
    /// </summary>
    public string PriorityName => PriorityId switch
    {
        1 => "Low",
        2 => "Medium",
        3 => "High",
        4 => "Critical",
        _ => ""
    };

    /// <summary>
    /// Gets the human-readable type name based on the type identifier.
    /// </summary>
    public string TypeName => TypeId switch
    {
        1 => "Acceptance",
        2 => "Accessibility",
        3 => "Automated",
        4 => "Compatibility",
        5 => "Destructive",
        6 => "Functional",
        7 => "Other",
        8 => "Performance",
        9 => "Regression",
        10 => "Security",
        11 => "Smoke & Sanity",
        12 => "Usability",
        _ => ""
    };
}
