using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>
/// Page model for the Setup page to configure TestRail connection.
/// Protected by an optional username/password defined in settings.
/// </summary>
public class SetupModel : PageModel
{
    private const string SessionKey = "SetupAuthenticated";

    private readonly SettingsService _settingsService;
    private readonly PermissionService _permissionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetupModel"/> class.
    /// </summary>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="permissionService">The permission service.</param>
    public SetupModel(SettingsService settingsService, PermissionService permissionService)
    {
        _settingsService = settingsService;
        _permissionService = permissionService;
    }

    /// <summary>
    /// Gets or sets the TestRail settings.
    /// </summary>
    [BindProperty]
    public TestRailSettings Settings { get; set; } = new();

    /// <summary>
    /// Gets or sets the error message if setup fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the login gate should be shown.
    /// </summary>
    public bool RequiresLogin { get; set; }

    /// <summary>
    /// Gets or sets the login username entered by the user.
    /// </summary>
    [BindProperty]
    public string? LoginUsername { get; set; }

    /// <summary>
    /// Gets or sets the login password entered by the user.
    /// </summary>
    [BindProperty]
    public string? LoginPassword { get; set; }

    /// <summary>
    /// Handles GET requests to load existing settings.
    /// Shows a login form when Setup credentials are configured and the session is not authenticated.
    /// </summary>
    public async Task OnGetAsync()
    {
        if (await IsLoginRequiredAsync())
        {
            RequiresLogin = true;
            return;
        }

        var existing = await _settingsService.GetSettingsAsync();
        if (existing is not null)
        {
            Settings = existing;
        }
    }

    /// <summary>
    /// Handles the login form POST.
    /// </summary>
    public async Task<IActionResult> OnPostLoginAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        if (settings is null || !IsProtected(settings))
        {
            return RedirectToPage();
        }

        if (string.Equals(LoginUsername, settings.SetupUsername, StringComparison.Ordinal)
            && string.Equals(LoginPassword, settings.SetupPassword, StringComparison.Ordinal))
        {
            HttpContext.Session.SetString(SessionKey, "true");
            return RedirectToPage();
        }

        RequiresLogin = true;
        ErrorMessage = "Invalid username or password.";
        return Page();
    }

    /// <summary>
    /// Handles POST requests to save settings.
    /// </summary>
    /// <returns>Redirect to Index on success, or the page with errors.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        if (await IsLoginRequiredAsync())
        {
            RequiresLogin = true;
            ErrorMessage = "Please log in first.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Settings.BaseUrl))
        {
            ErrorMessage = "TestRail URL is required.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Settings.Username))
        {
            ErrorMessage = "Email is required.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Settings.ApiKey))
        {
            ErrorMessage = "API Key is required.";
            return Page();
        }

        // Preserve existing Setup credentials when saving — the login fields are not on the settings form.
        var existing = await _settingsService.GetSettingsAsync();
        if (existing is not null)
        {
            Settings.SetupUsername = existing.SetupUsername;
            Settings.SetupPassword = existing.SetupPassword;
            Settings.DatabasePassword = existing.DatabasePassword;
        }

        try
        {
            await _settingsService.SaveSettingsAsync(Settings);
            _permissionService.ClearCache();
            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save settings: {ex.Message}";
            return Page();
        }
    }

    /// <summary>
    /// Determines whether the Setup page is password-protected.
    /// </summary>
    private static bool IsProtected(TestRailSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.SetupUsername)
            && !string.IsNullOrWhiteSpace(settings.SetupPassword);
    }

    /// <summary>
    /// Checks whether the current request requires login.
    /// </summary>
    private async Task<bool> IsLoginRequiredAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        if (settings is null || !IsProtected(settings))
        {
            return false;
        }

        return HttpContext.Session.GetString(SessionKey) != "true";
    }
}
