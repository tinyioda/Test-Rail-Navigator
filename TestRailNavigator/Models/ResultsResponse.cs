using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Response wrapper for the get_results API endpoint.
/// </summary>
public class ResultsResponse
{
    /// <summary>
    /// Gets or sets the offset for pagination.
    /// </summary>
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    /// <summary>
    /// Gets or sets the limit for pagination.
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    /// <summary>
    /// Gets or sets the total size of results.
    /// </summary>
    [JsonPropertyName("size")]
    public int Size { get; set; }

    /// <summary>
    /// Gets or sets the list of test results.
    /// </summary>
    [JsonPropertyName("results")]
    public List<TestResult> Results { get; set; } = [];
}
