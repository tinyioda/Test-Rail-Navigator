using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Represents a single entry from TestRail's <c>custom_steps_separated</c>
/// field: a step instruction paired with its expected outcome.
/// </summary>
public class TestStep
{
    /// <summary>
    /// Gets or sets the step instruction (what the tester should do).
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets the expected result for this individual step.
    /// </summary>
    [JsonPropertyName("expected")]
    public string? Expected { get; set; }
}
