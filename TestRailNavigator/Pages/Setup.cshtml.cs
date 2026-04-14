using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>
/// Page model for the Setup page to configure TestRail connection.
/// </summary>
public class SetupModel : PageModel
{
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
    /// Handles GET requests to load existing settings.
    /// </summary>
    public async Task OnGetAsync()
    {
        var existing = await _settingsService.GetSettingsAsync();
        if (existing is not null)
        {
            Settings = existing;
        }
    }

    /// <summary>
    /// Handles POST requests to save settings.
    /// </summary>
    /// <returns>Redirect to Index on success, or the page with errors.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
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
}
