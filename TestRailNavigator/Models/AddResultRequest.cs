using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Request model for adding a test result via the TestRail add_result_for_case endpoint.
/// </summary>
public class AddResultRequest
{
    /// <summary>
    /// Gets or sets the status identifier for the result.
    /// 1 = Passed, 2 = Blocked, 3 = Untested, 4 = Retest, 5 = Failed.
    /// </summary>
    [JsonPropertyName("status_id")]
    public int StatusId { get; set; }

    /// <summary>
    /// Gets or sets the optional comment for the result.
    /// </summary>
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}
