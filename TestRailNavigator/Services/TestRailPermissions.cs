namespace TestRailNavigator.Services;

/// <summary>
/// Represents the current user's permissions derived from their TestRail role.
/// Used to gate UI actions without exposing raw role identifiers to pages.
/// </summary>
public class TestRailPermissions
{
    /// <summary>
    /// Gets or sets a value indicating whether the user can view projects, runs, and results.
    /// All roles have this permission.
    /// </summary>
    public bool CanRead { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user can add test results.
    /// Tester and above.
    /// </summary>
    public bool CanAddResults { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user can create and edit test cases.
    /// Designer and above.
    /// </summary>
    public bool CanManageCases { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user can create/edit/delete runs, plans, and milestones.
    /// Lead and above.
    /// </summary>
    public bool CanManageRuns { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has full administrative access.
    /// Admin only.
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Gets or sets the display name of the current user.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role name of the current user.
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Creates a default read-only permission set used when the user cannot be resolved.
    /// </summary>
    public static TestRailPermissions ReadOnly() => new()
    {
        CanRead = true,
        CanAddResults = false,
        CanManageCases = false,
        CanManageRuns = false,
        IsAdmin = false,
        UserName = "Unknown",
        RoleName = "Read-only"
    };

    /// <summary>
    /// Creates a permission set from a TestRail role identifier.
    /// </summary>
    /// <param name="roleId">The TestRail role identifier.</param>
    /// <param name="isAdmin">Whether the user is flagged as admin.</param>
    /// <param name="userName">The display name of the user.</param>
    /// <param name="roleName">The human-readable role name.</param>
    public static TestRailPermissions FromRole(int roleId, bool isAdmin, string userName, string roleName) => new()
    {
        CanRead = true,
        CanAddResults = isAdmin || roleId >= 2,
        CanManageCases = isAdmin || roleId >= 3,
        CanManageRuns = isAdmin || roleId >= 4,
        IsAdmin = isAdmin || roleId >= 5,
        UserName = userName,
        RoleName = roleName
    };
}
