using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace TestRailNavigator.Services;

/// <summary>
/// Authenticates the single administrator using deployment configuration or existing Setup credentials.
/// </summary>
public sealed class AdminAuthenticationService(SettingsService settingsService, IConfiguration configuration)
{
    /// <summary>Gets the role assigned to the application's single administrator.</summary>
    public const string AdministratorRole = "Administrator";

    /// <summary>Gets the policy required for administrative pages.</summary>
    public const string AdministratorPolicy = "Administrator";

    /// <summary>Gets the rate-limiting policy applied to sign-in attempts.</summary>
    public const string LoginRateLimitPolicy = "AdminLogin";

    private const string CredentialVersionClaim = "credential-version";

    /// <summary>Determines whether both administrator credentials have been provisioned.</summary>
    public async Task<bool> IsConfiguredAsync() => await GetCredentialsAsync() is not null;

    /// <summary>Authenticates supplied credentials without exposing their values in the identity.</summary>
    public async Task<ClaimsPrincipal?> AuthenticateAsync(string? username, string? password)
    {
        var credentials = await GetCredentialsAsync();
        if (credentials is null)
        {
            return null;
        }

        var validUsername = FixedTimeEquals(username, credentials.Value.Username);
        var validPassword = FixedTimeEquals(password, credentials.Value.Password);
        if (!validUsername || !validPassword)
        {
            return null;
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, credentials.Value.Username),
            new Claim(ClaimTypes.Role, AdministratorRole),
            new Claim(CredentialVersionClaim, GetCredentialVersion(credentials.Value))
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>Rejects identities issued before administrator credentials changed or were removed.</summary>
    public async Task<bool> IsPrincipalValidAsync(ClaimsPrincipal? principal)
    {
        var credentials = await GetCredentialsAsync();
        return credentials is not null
            && principal?.Identity?.IsAuthenticated == true
            && principal.IsInRole(AdministratorRole)
            && FixedTimeEquals(principal.Identity.Name, credentials.Value.Username)
            && FixedTimeEquals(
                principal.FindFirstValue(CredentialVersionClaim),
                GetCredentialVersion(credentials.Value));
    }

    /// <summary>Resolves deployment-provided credentials before falling back to the settings file.</summary>
    private async Task<(string Username, string Password)?> GetCredentialsAsync()
    {
        var username = configuration["TestRail:SetupUsername"];
        var password = configuration["TestRail:SetupPassword"];
        if (username is null || password is null)
        {
            var settings = await settingsService.GetSettingsAsync();
            username ??= settings?.SetupUsername;
            password ??= settings?.SetupPassword;
        }

        return string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)
            ? null
            : (username, password);
    }

    /// <summary>Compares fixed-length hashes without revealing which credential differed.</summary>
    private static bool FixedTimeEquals(string? supplied, string expected)
    {
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied ?? string.Empty));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }

    /// <summary>Creates an unambiguous credential stamp stored only inside the protected auth ticket.</summary>
    private static string GetCredentialVersion((string Username, string Password) credentials) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{credentials.Username.Length}:{credentials.Username}{credentials.Password}")));
}
