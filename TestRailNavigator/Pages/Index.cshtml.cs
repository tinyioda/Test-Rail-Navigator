using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Models;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>
/// Page model for the Index page displaying all TestRail projects.
/// </summary>
public class IndexModel : PageModel
{
    private readonly TestRailClient _testRail;
    private readonly SettingsService _settingsService;
    private readonly ConsoleLogService _consoleLog;
    private readonly PermissionService _permissionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    /// <param name="testRail">The TestRail client service.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="consoleLog">The console log service.</param>
    /// <param name="permissionService">The permission service.</param>
    public IndexModel(TestRailClient testRail, SettingsService settingsService, ConsoleLogService consoleLog, PermissionService permissionService)
    {
        _testRail = testRail;
        _settingsService = settingsService;
        _consoleLog = consoleLog;
        _permissionService = permissionService;
    }

    /// <summary>
    /// Gets or sets the current user's permissions.
    /// </summary>
    public TestRailPermissions Permissions { get; set; } = TestRailPermissions.ReadOnly();

    /// <summary>
    /// Gets or sets the list of projects to display.
    /// </summary>
    public List<Project> Projects { get; set; } = [];

    /// <summary>
    /// Gets or sets the error message if loading fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the success message after an action.
    /// </summary>
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether write operations are enabled.
    /// </summary>
    public bool WritesEnabled { get; set; }

    /// <summary>
    /// Gets or sets the new project request for the create form.
    /// </summary>
    [BindProperty]
    public CreateProjectRequest NewProject { get; set; } = new();

    /// <summary>
    /// Handles GET requests to load all projects.
    /// </summary>
    /// <returns>The page result or redirect to setup.</returns>
    public async Task<IActionResult> OnGetAsync()
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        try
        {
            Projects = (await _testRail.GetProjectsAsync()).OrderBy(p => p.Name).ToList();
            Permissions = await _permissionService.GetPermissionsAsync();
            WritesEnabled = await _settingsService.AreWritesEnabledAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load projects: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        return Page();
    }

    /// <summary>
    /// Handles POST requests to create a new project.
    /// </summary>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        _consoleLog.Log("OnPostAsync called");

        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadProjectsAsync();
            Permissions = await _permissionService.GetPermissionsAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewProject.Name))
        {
            ErrorMessage = "Project name is required.";
            _consoleLog.Log(ErrorMessage);
            await LoadProjectsAsync();
            return Page();
        }

        _consoleLog.Log($"Creating project: {NewProject.Name}");

        try
        {
            var created = await _testRail.CreateProjectAsync(NewProject);
            SuccessMessage = $"Project '{created?.Name}' created successfully.";
            _consoleLog.Log(SuccessMessage);
            NewProject = new();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create project: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadProjectsAsync();
        Permissions = await _permissionService.GetPermissionsAsync();
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
        return Page();
    }

    /// <summary>
    /// Loads the projects list.
    /// </summary>
    private async Task LoadProjectsAsync()
    {
        try
        {
            Projects = (await _testRail.GetProjectsAsync()).OrderBy(p => p.Name).ToList();
        }
        catch
        {
            // Keep existing error message if set
        }
    }
}
