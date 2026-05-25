using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Request model for creating a new test case via the TestRail add_case endpoint.
/// </summary>
public class AddTestCaseRequest
{
    /// <summary>
    /// Gets or sets the title of the test case (required).
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the template identifier (e.g. 1 = Test Case (Text), 2 = Test Case (Steps), 3 = Exploratory Session).
    /// </summary>
    [JsonPropertyName("template_id")]
    public int? TemplateId { get; set; }

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

    /// <summary>
    /// Gets or sets the summary shown on the "Test Case (Steps)" template. Maps to the
    /// <c>custom_summary</c> field in TestRail. Ignored on the plain "Test Case (Text)" template.
    /// </summary>
    [JsonPropertyName("custom_summary")]
    public string? Summary { get; set; }

    /// <summary>
    /// Gets or sets the separated step/expected pairs used by the "Test Case (Steps)" template
    /// (TemplateId = 2). When populated, each pair becomes a row in TestRail's Steps grid with
    /// its own Expected Result column. When null, the case falls back to the plain
    /// <see cref="Steps"/>/<see cref="ExpectedResult"/> fields.
    /// </summary>
    [JsonPropertyName("custom_steps_separated")]
    public IList<TestCaseStep>? StepsSeparated { get; set; }
}

/// <summary>
/// One row in the "Test Case (Steps)" grid.
/// </summary>
public class TestCaseStep
{
    /// <summary>Gets or sets the step instruction (what the tester does).</summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the expected result for this step.</summary>
    [JsonPropertyName("expected")]
    public string Expected { get; set; } = string.Empty;
}
