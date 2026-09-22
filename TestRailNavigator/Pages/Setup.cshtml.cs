using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>
/// Page model for the Setup page to configure TestRail connection.
/// Restricted to the authenticated application administrator.
/// </summary>
[Authorize(Policy = AdminAuthenticationService.AdministratorPolicy)]
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

        // Preserve existing Setup credentials when saving — the login fields are not on the settings form.
        var existing = await _settingsService.GetSettingsAsync();
        Settings.SetupUsername = existing?.SetupUsername ?? string.Empty;
        Settings.SetupPassword = existing?.SetupPassword ?? string.Empty;
        Settings.DatabasePassword = existing?.DatabasePassword ?? string.Empty;
        if (existing is not null)
        {
            // Preserve the existing Azure DevOps PAT when the user submits an empty value,
            // so that re-saving the form without re-typing the token does not clear it.
            if (string.IsNullOrWhiteSpace(Settings.AzureDevOpsPat))
            {
                Settings.AzureDevOpsPat = existing.AzureDevOpsPat;
            }

            // Same preserve-on-blank behavior for the Jira API token.
            if (string.IsNullOrWhiteSpace(Settings.JiraApiToken))
            {
                Settings.JiraApiToken = existing.JiraApiToken;
            }

            // Same preserve-on-blank behavior for the OpenAI-compatible API key.
            if (string.IsNullOrWhiteSpace(Settings.OpenAiApiKey))
            {
                Settings.OpenAiApiKey = existing.OpenAiApiKey;
            }
        }

        if ((!string.IsNullOrWhiteSpace(Settings.AzureDevOpsBaseUrl)
                || !string.IsNullOrWhiteSpace(Settings.AzureDevOpsPat))
            && !AzureDevOpsUrlPolicy.TryGetBaseUri(Settings.AzureDevOpsBaseUrl, out _))
        {
            ErrorMessage = "Azure DevOps requires an approved HTTPS organization or collection base URL, without a query, fragment, or embedded credentials.";
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
