using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Request model for adding an entry to a TestRail test plan.
/// </summary>
public class TestPlanEntryRequest
{
    /// <summary>
    /// Gets or sets the suite identifier to create runs from (required).
    /// </summary>
    [JsonPropertyName("suite_id")]
    public int SuiteId { get; set; }

    /// <summary>
    /// Gets or sets the name of the entry. Defaults to the suite name if omitted.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets whether to include all test cases from the suite.
    /// Defaults to false so new runs start empty.
    /// </summary>
    [JsonPropertyName("include_all")]
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the specific case identifiers to include.
    /// Only used when <see cref="IncludeAll"/> is false.
    /// </summary>
    [JsonPropertyName("case_ids")]
    public List<int>? CaseIds { get; set; }
}
