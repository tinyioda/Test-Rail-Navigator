using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TestRailNavigator.Pages;

/// <summary>Ends an administrator session only after an authenticated, antiforgery-protected POST.</summary>
public class LogoutModel : PageModel
{
    /// <summary>Keeps ordinary link navigation from changing authentication state.</summary>
    public IActionResult OnGet() => RedirectToPage("/Index");

    /// <summary>Clears the authentication cookie and returns to sign-in.</summary>
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }
}
