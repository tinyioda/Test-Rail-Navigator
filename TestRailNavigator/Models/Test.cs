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
}
