using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Represents a TestRail section (a folder within a suite that organizes test cases).
/// </summary>
public class Section
{
    /// <summary>
    /// Gets or sets the unique identifier of the section.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the suite identifier this section belongs to.
    /// </summary>
    [JsonPropertyName("suite_id")]
    public int SuiteId { get; set; }

    /// <summary>
    /// Gets or sets the name of the section.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the section.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the parent section identifier, if nested.
    /// </summary>
    [JsonPropertyName("parent_id")]
    public int? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the nesting depth (0 = top-level).
    /// </summary>
    [JsonPropertyName("depth")]
    public int Depth { get; set; }

    /// <summary>
    /// Gets or sets the display order of the section.
    /// </summary>
    [JsonPropertyName("display_order")]
    public int DisplayOrder { get; set; }
}
