using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace TestRailNavigator.Services;

/// <summary>Invalidates administrator cookies when the configured credentials change.</summary>
public sealed class AdminCookieEvents(AdminAuthenticationService authentication) : CookieAuthenticationEvents
{
    /// <inheritdoc />
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!await authentication.IsPrincipalValidAsync(context.Principal))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
