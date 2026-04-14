using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Response wrapper for the get_cases API endpoint.
/// </summary>
public class CasesResponse
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
    /// Gets or sets the list of test cases.
    /// </summary>
    [JsonPropertyName("cases")]
    public List<TestCase> Cases { get; set; } = [];
}
