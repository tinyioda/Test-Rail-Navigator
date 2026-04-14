using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Paginated response wrapper for the get_sections endpoint.
/// </summary>
public class SectionsResponse
{
    /// <summary>
    /// Gets or sets the list of sections.
    /// </summary>
    [JsonPropertyName("sections")]
    public List<Section> Sections { get; set; } = [];
}
