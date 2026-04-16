using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Request model for creating a new standalone test run.
/// </summary>
public class CreateRunRequest
{
    /// <summary>
    /// Gets or sets the suite identifier. Required for projects using multiple suites (suite_mode = 2 or 3).
    /// </summary>
    [JsonPropertyName("suite_id")]
    public int? SuiteId { get; set; }

    /// <summary>
    /// Gets or sets the name of the test run (required).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the test run.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the milestone identifier to associate with this run.
    /// </summary>
    [JsonPropertyName("milestone_id")]
    public int? MilestoneId { get; set; }

    /// <summary>
    /// Gets or sets whether to include all test cases from the suite.
    /// </summary>
    [JsonPropertyName("include_all")]
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the specific case identifiers to include in the run.
    /// Only used when <see cref="IncludeAll"/> is false.
    /// </summary>
    [JsonPropertyName("case_ids")]
    public List<int>? CaseIds { get; set; }
}
