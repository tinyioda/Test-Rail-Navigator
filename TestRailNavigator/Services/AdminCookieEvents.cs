using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace TestRailNavigator.Services;

/// <summary>Invalidates sessions when their server-side TestRail credentials are no longer available,
/// and otherwise restores those credentials for use by <see cref="TestRailClient"/> this request.</summary>
public sealed class AdminCookieEvents(AdminAuthenticationService authentication) : CookieAuthenticationEvents
{
    /// <inheritdoc />
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!await authentication.IsPrincipalValidAsync(context.Principal))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        authentication.ApplySessionCredentials(context.Principal);
    }
}
