using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Response wrapper for the get_suites API endpoint.
/// </summary>
public class SuitesResponse
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
    /// Gets or sets the list of suites.
    /// </summary>
    [JsonPropertyName("suites")]
    public List<Suite> Suites { get; set; } = [];
}
