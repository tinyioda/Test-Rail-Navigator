using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Represents an entry within a TestRail test plan.
/// Each entry corresponds to a suite and contains one or more test runs.
/// </summary>
public class TestPlanEntry
{
    /// <summary>
    /// Gets or sets the unique identifier of the entry.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the suite identifier this entry is based on.
    /// </summary>
    [JsonPropertyName("suite_id")]
    public int SuiteId { get; set; }

    /// <summary>
    /// Gets or sets the name of the entry.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the test runs within this entry.
    /// </summary>
    [JsonPropertyName("runs")]
    public List<TestRun> Runs { get; set; } = [];
}
