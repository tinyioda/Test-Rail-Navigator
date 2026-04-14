using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Request model for updating a test run's case selection.
/// Used by both update_run and update_plan_entry endpoints.
/// </summary>
public class UpdateRunRequest
{
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
    public List<int> CaseIds { get; set; } = [];
}
