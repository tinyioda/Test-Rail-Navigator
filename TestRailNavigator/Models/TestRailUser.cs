using System.Text.Json.Serialization;

namespace TestRailNavigator.Models;

/// <summary>
/// Represents the currently authenticated TestRail user.
/// </summary>
public class TestRailUser
{
    /// <summary>
    /// Gets or sets the unique identifier of the user.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address of the user.
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role identifier of the user.
    /// </summary>
    /// <remarks>
    /// Standard TestRail roles:
    /// 1 = Read-only, 2 = Tester, 3 = Designer, 4 = Lead, 5 = Admin.
    /// Custom roles may use higher identifiers.
    /// </remarks>
    [JsonPropertyName("role_id")]
    public int RoleId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user account is active.
    /// </summary>
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is an administrator.
    /// </summary>
    [JsonPropertyName("is_admin")]
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Gets the human-readable role name based on the role identifier.
    /// </summary>
    public string RoleName => RoleId switch
    {
        1 => "Read-only",
        2 => "Tester",
        3 => "Designer",
        4 => "Lead",
        5 => "Admin",
        _ => IsAdmin ? "Admin" : "Custom"
    };
}
