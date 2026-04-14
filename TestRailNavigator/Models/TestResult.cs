using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Represents a TestRail test result.
/// </summary>
public class TestResult
{
    /// <summary>
    /// Gets or sets the unique identifier of the result.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the test identifier this result belongs to.
    /// </summary>
    [JsonPropertyName("test_id")]
    public int TestId { get; set; }

    /// <summary>
    /// Gets or sets the status identifier of the result.
    /// </summary>
    [JsonPropertyName("status_id")]
    public int StatusId { get; set; }

    /// <summary>
    /// Gets or sets the comment associated with the result.
    /// </summary>
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    /// <summary>
    /// Gets or sets the Unix timestamp when the result was created.
    /// </summary>
    [JsonPropertyName("created_on")]
    public long CreatedOn { get; set; }

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
