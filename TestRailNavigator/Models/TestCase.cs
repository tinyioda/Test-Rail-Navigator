using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Represents a TestRail test case (a permanent definition in the case library).
/// This is distinct from <see cref="Test"/>, which is a run-time instance of a test case within a test run.
/// </summary>
public class TestCase
{
    /// <summary>
    /// Gets or sets the unique identifier of the test case.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the title of the test case.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the section identifier this case belongs to.
    /// </summary>
    [JsonPropertyName("section_id")]
    public int? SectionId { get; set; }

    /// <summary>
    /// Gets or sets the suite identifier this case belongs to.
    /// </summary>
    [JsonPropertyName("suite_id")]
    public int? SuiteId { get; set; }

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
    /// Gets or sets the identifier of the user the test case is assigned to.
    /// </summary>
    [JsonPropertyName("created_by")]
    public int? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp as a Unix epoch.
    /// </summary>
    [JsonPropertyName("created_on")]
    public long? CreatedOn { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who last updated the test case.
    /// </summary>
    [JsonPropertyName("updated_by")]
    public int? UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp as a Unix epoch.
    /// </summary>
    [JsonPropertyName("updated_on")]
    public long? UpdatedOn { get; set; }

    /// <summary>
    /// Gets or sets the external references (e.g. JIRA ticket IDs).
    /// </summary>
    [JsonPropertyName("refs")]
    public string? Refs { get; set; }

    /// <summary>
    /// Gets or sets the milestone identifier the test case belongs to.
    /// </summary>
    [JsonPropertyName("milestone_id")]
    public int? MilestoneId { get; set; }

    /// <summary>
    /// Gets or sets the template identifier.
    /// </summary>
    [JsonPropertyName("template_id")]
    public int? TemplateId { get; set; }

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
    /// Gets the human-readable priority name based on the priority identifier.
    /// </summary>
    public string PriorityName => PriorityId switch
    {
        1 => "Low",
        2 => "Medium",
        3 => "High",
        4 => "Critical",
        _ => "Unknown"
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
