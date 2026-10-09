using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>Ends a signed-in user's session only after an authenticated, antiforgery-protected POST.</summary>
public class LogoutModel(AdminAuthenticationService authentication) : PageModel
{
    /// <summary>Keeps ordinary link navigation from changing authentication state.</summary>
    public IActionResult OnGet() => RedirectToPage("/Index");

    /// <summary>Clears the authentication cookie, discards the server-side TestRail session, and returns to sign-in.</summary>
    public async Task<IActionResult> OnPostAsync()
    {
        authentication.EndSession(User);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }
}
