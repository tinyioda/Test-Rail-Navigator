using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>Signs in the provisioned administrator before any integration can be accessed.</summary>
[EnableRateLimiting(AdminAuthenticationService.LoginRateLimitPolicy)]
public class LoginModel(AdminAuthenticationService authentication, ILogger<LoginModel> logger) : PageModel
{
    /// <summary>Gets or sets the administrator username.</summary>
    [BindProperty, Required, StringLength(128)]
    public string? Username { get; set; }

    /// <summary>Gets or sets the administrator password supplied for this request only.</summary>
    [BindProperty, Required, StringLength(1024), DataType(DataType.Password)]
    public string? Password { get; set; }

    /// <summary>Gets or sets the local page to open after sign-in.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>Gets a value indicating whether server-side credentials are provisioned.</summary>
    public bool IsConfigured { get; private set; }

    /// <summary>Gets a safe sign-in error message.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Displays sign-in or a fail-closed provisioning notice.</summary>
    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectLocally();
        }

        IsConfigured = await authentication.IsConfiguredAsync();
        if (!IsConfigured)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        return Page();
    }

    /// <summary>Authenticates credentials and issues a short-lived, protected administrator cookie.</summary>
    public async Task<IActionResult> OnPostAsync()
    {
        IsConfigured = await authentication.IsConfiguredAsync();
        if (!IsConfigured)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Page();
        }

        var principal = ModelState.IsValid
            ? await authentication.AuthenticateAsync(Username, Password)
            : null;
        Password = null;
        ModelState.Remove(nameof(Password));
        if (principal is null)
        {
            logger.LogWarning("Administrator sign-in rejected.");
            ErrorMessage = "Invalid username or password.";
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
        logger.LogInformation("Administrator signed in.");
        return RedirectLocally();
    }

    /// <summary>Prevents a supplied return URL from redirecting credentials or navigation off-site.</summary>
    private IActionResult RedirectLocally() =>
        ReturnUrl is not null && Url.IsLocalUrl(ReturnUrl)
            ? LocalRedirect(ReturnUrl)
            : RedirectToPage("/Index");
}
