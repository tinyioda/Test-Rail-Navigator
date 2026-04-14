using TestRailNavigator.Models;

namespace TestRailNavigator.Services;

/// <summary>
/// Service that resolves and caches the current user's permissions from TestRail.
/// Permissions are fetched once per configuration and cached until settings change.
/// </summary>
public class PermissionService
{
    private readonly TestRailClient _testRail;
    private readonly ConsoleLogService _consoleLog;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TestRailPermissions? _cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionService"/> class.
    /// </summary>
    /// <param name="testRail">The TestRail client service.</param>
    /// <param name="consoleLog">The console log service.</param>
    public PermissionService(TestRailClient testRail, ConsoleLogService consoleLog)
    {
        _testRail = testRail;
        _consoleLog = consoleLog;
    }

    /// <summary>
    /// Gets the current user's permissions. Fetches from TestRail on first call and caches the result.
    /// </summary>
    /// <returns>The resolved permissions, or read-only defaults if the user cannot be resolved.</returns>
    public async Task<TestRailPermissions> GetPermissionsAsync()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _lock.WaitAsync();
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            var user = await _testRail.GetCurrentUserAsync();

            if (user is null)
            {
                _consoleLog.Log("Could not resolve current user — defaulting to read-only permissions.");
                _cached = TestRailPermissions.ReadOnly();
                return _cached;
            }

            _cached = TestRailPermissions.FromRole(user.RoleId, user.IsAdmin, user.Name, user.RoleName);
            _consoleLog.Log($"Permissions loaded: {user.Name} ({_cached.RoleName}) — " +
                $"Read={_cached.CanRead}, Results={_cached.CanAddResults}, " +
                $"Cases={_cached.CanManageCases}, Runs={_cached.CanManageRuns}, Admin={_cached.IsAdmin}");

            return _cached;
        }
        catch (Exception ex)
        {
            _consoleLog.Log($"Failed to load permissions: {ex.Message} — defaulting to read-only.");
            _cached = TestRailPermissions.ReadOnly();
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Clears the cached permissions, forcing a re-fetch on the next request.
    /// Should be called when settings are changed (e.g., different user logs in).
    /// </summary>
    public void ClearCache()
    {
        _cached = null;
    }
}
