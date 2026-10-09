using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Unit coverage for TestRail role-to-permission mapping.</summary>
public class TestRailPermissionsTests
{
    /// <summary>The default fallback permission set is read-only and identifies the user generically.</summary>
    [Fact]
    public void ReadOnly_ReturnsExpectedDefaults()
    {
        var permissions = TestRailPermissions.ReadOnly();

        Assert.True(permissions.CanRead);
        Assert.False(permissions.CanAddResults);
        Assert.False(permissions.CanManageCases);
        Assert.False(permissions.CanManageRuns);
        Assert.False(permissions.IsAdmin);
        Assert.Equal("Unknown", permissions.UserName);
        Assert.Equal("Read-only", permissions.RoleName);
    }

    /// <summary>Each standard TestRail role unlocks the expected permission threshold.</summary>
    [Theory]
    [InlineData(1, false, false, false, false)]
    [InlineData(2, true, false, false, false)]
    [InlineData(3, true, true, false, false)]
    [InlineData(4, true, true, true, false)]
    [InlineData(5, true, true, true, true)]
    public void FromRole_MapsStandardThresholds(int roleId, bool canAddResults, bool canManageCases, bool canManageRuns, bool isAdmin)
    {
        var permissions = TestRailPermissions.FromRole(roleId, false, "Casey", "Role");

        Assert.True(permissions.CanRead);
        Assert.Equal(canAddResults, permissions.CanAddResults);
        Assert.Equal(canManageCases, permissions.CanManageCases);
        Assert.Equal(canManageRuns, permissions.CanManageRuns);
        Assert.Equal(isAdmin, permissions.IsAdmin);
        Assert.Equal("Casey", permissions.UserName);
        Assert.Equal("Role", permissions.RoleName);
    }

    /// <summary>An explicit admin flag elevates every permission even when the numeric role is low.</summary>
    [Fact]
    public void FromRole_UsesExplicitAdminFlagAsAnOverride()
    {
        var permissions = TestRailPermissions.FromRole(1, true, "Admin User", "Custom Admin");

        Assert.True(permissions.CanRead);
        Assert.True(permissions.CanAddResults);
        Assert.True(permissions.CanManageCases);
        Assert.True(permissions.CanManageRuns);
        Assert.True(permissions.IsAdmin);
        Assert.Equal("Admin User", permissions.UserName);
        Assert.Equal("Custom Admin", permissions.RoleName);
    }
}
