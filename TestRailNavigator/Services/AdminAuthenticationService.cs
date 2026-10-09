using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using TestRailNavigator.Models;

namespace TestRailNavigator.Services;

/// <summary>
/// Authenticates application users by validating their credentials live against the configured
/// TestRail instance, rather than a single shared administrator account. Any TestRail user whose
/// username/password (or personal API key) TestRail accepts can sign in; their own TestRail role
/// then governs what they can do inside the app (see <see cref="PermissionService"/>).
/// </summary>
public sealed class AdminAuthenticationService(
    SettingsService settingsService,
    TestRailClient testRailClient,
    TestRailCredentialStore credentialStore,
    CurrentTestRailCredentials currentCredentials)
{
    /// <summary>Gets the policy required for application pages (any signed-in TestRail user).</summary>
    public const string AdministratorPolicy = "Authenticated";

    /// <summary>Gets the rate-limiting policy applied to sign-in attempts.</summary>
    public const string LoginRateLimitPolicy = "AdminLogin";

    private const string SessionTokenClaim = "trn-session";

    /// <summary>Determines whether the TestRail connection is configured, which is required before anyone can sign in.</summary>
    public async Task<bool> IsConfiguredAsync() => await settingsService.IsConfiguredAsync();

    /// <summary>
    /// Validates the supplied username/password live against TestRail. On success, stores those
    /// credentials server-side under a new opaque session token and returns a principal carrying
    /// only that token (never the credentials themselves).
    /// </summary>
    public async Task<ClaimsPrincipal?> AuthenticateAsync(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)
            || !await IsConfiguredAsync())
        {
            return null;
        }

        currentCredentials.Username = username;
        currentCredentials.Secret = password;
        TestRailUser? user;
        try
        {
            user = await testRailClient.GetCurrentUserAsync();
        }
        catch
        {
            user = null;
        }
        finally
        {
            currentCredentials.Username = null;
            currentCredentials.Secret = null;
        }

        if (user is null)
        {
            return null;
        }

        var token = credentialStore.Store(username, password);
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, user.Name ?? username),
            new Claim(SessionTokenClaim, token)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>Rejects identities whose server-side credential session no longer exists (signed out,
    /// or the app restarted since they signed in).</summary>
    public Task<bool> IsPrincipalValidAsync(ClaimsPrincipal? principal)
    {
        var valid = principal?.Identity?.IsAuthenticated == true
            && credentialStore.Contains(principal.FindFirstValue(SessionTokenClaim));
        return Task.FromResult(valid);
    }

    /// <summary>Removes the server-side credential session bound to the given principal, if any.</summary>
    public void EndSession(ClaimsPrincipal? principal) =>
        credentialStore.Remove(principal?.FindFirstValue(SessionTokenClaim));

    /// <summary>
    /// Populates <see cref="CurrentTestRailCredentials"/> for the current request from the signed-in
    /// principal's server-side session, so <see cref="TestRailClient"/> uses that user's own
    /// credentials instead of the configured service account.
    /// </summary>
    public void ApplySessionCredentials(ClaimsPrincipal? principal)
    {
        var credentials = credentialStore.Get(principal?.FindFirstValue(SessionTokenClaim));
        if (credentials is not null)
        {
            currentCredentials.Username = credentials.Value.Username;
            currentCredentials.Secret = credentials.Value.Secret;
        }
    }
}
