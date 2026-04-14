using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Models;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>
/// Page model for the Milestones page with full CRUD support.
/// </summary>
public class MilestonesModel : PageModel
{
    private readonly TestRailClient _testRail;
    private readonly SettingsService _settingsService;
    private readonly ConsoleLogService _consoleLog;
    private readonly PermissionService _permissionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MilestonesModel"/> class.
    /// </summary>
    /// <param name="testRail">The TestRail client service.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="consoleLog">The console log service.</param>
    /// <param name="permissionService">The permission service.</param>
    public MilestonesModel(TestRailClient testRail, SettingsService settingsService, ConsoleLogService consoleLog, PermissionService permissionService)
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
    /// Gets or sets the current project.
    /// </summary>
    public Project? Project { get; set; }

    /// <summary>
    /// Gets or sets the project identifier from the route.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the list of milestones to display.
    /// </summary>
    public List<Milestone> Milestones { get; set; } = [];

    /// <summary>
    /// Gets or sets the error message if an operation fails.
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
    /// Gets or sets the milestone name for the create/edit form.
    /// </summary>
    [BindProperty]
    public string MilestoneName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the milestone description for the create/edit form.
    /// </summary>
    [BindProperty]
    public string? MilestoneDescription { get; set; }

    /// <summary>
    /// Gets or sets the milestone start date for the create/edit form.
    /// </summary>
    [BindProperty]
    public DateTime? MilestoneStartDate { get; set; }

    /// <summary>
    /// Gets or sets the milestone due date for the create/edit form.
    /// </summary>
    [BindProperty]
    public DateTime? MilestoneDueDate { get; set; }

    /// <summary>
    /// Gets or sets the milestone identifier for update/delete operations.
    /// </summary>
    [BindProperty]
    public int MilestoneId { get; set; }

    /// <summary>
    /// Gets or sets whether the milestone is completed (for update).
    /// </summary>
    [BindProperty]
    public bool MilestoneIsCompleted { get; set; }

    /// <summary>
    /// Handles GET requests to load milestones for a project.
    /// </summary>
    /// <returns>The page result or redirect to setup.</returns>
    public async Task<IActionResult> OnGetAsync()
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        await LoadPageDataAsync();
        return Page();
    }

    /// <summary>
    /// Handles POST requests to create a new milestone.
    /// </summary>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(MilestoneName))
        {
            ErrorMessage = "Milestone name is required.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync();
            return Page();
        }

        _consoleLog.Log($"Creating milestone: {MilestoneName}");

        try
        {
            var request = BuildRequest();
            var created = await _testRail.AddMilestoneAsync(ProjectId, request);
            SuccessMessage = $"Milestone '{created?.Name}' created successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create milestone: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync();
        return Page();
    }

    /// <summary>
    /// Handles POST requests to update an existing milestone.
    /// </summary>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostEditAsync()
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(MilestoneName))
        {
            ErrorMessage = "Milestone name is required.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync();
            return Page();
        }

        _consoleLog.Log($"Updating milestone {MilestoneId}: {MilestoneName}");

        try
        {
            var request = BuildRequest();
            request.IsCompleted = MilestoneIsCompleted;
            request.IsStarted = !MilestoneIsCompleted && MilestoneStartDate.HasValue && MilestoneStartDate.Value <= DateTime.Today;
            var updated = await _testRail.UpdateMilestoneAsync(MilestoneId, request);
            SuccessMessage = $"Milestone '{updated?.Name}' updated successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to update milestone: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync();
        return Page();
    }

    /// <summary>
    /// Handles POST requests to delete a milestone.
    /// </summary>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostDeleteAsync()
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync();
            return Page();
        }

        _consoleLog.Log($"Deleting milestone {MilestoneId}");

        try
        {
            await _testRail.DeleteMilestoneAsync(MilestoneId);
            SuccessMessage = "Milestone deleted successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete milestone: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync();
        return Page();
    }

    /// <summary>
    /// Builds a <see cref="MilestoneRequest"/> from the bound form properties.
    /// </summary>
    /// <returns>A populated milestone request.</returns>
    private MilestoneRequest BuildRequest()
    {
        return new MilestoneRequest
        {
            Name = MilestoneName,
            Description = MilestoneDescription,
            StartOn = MilestoneStartDate.HasValue
                ? new DateTimeOffset(MilestoneStartDate.Value, TimeSpan.Zero).ToUnixTimeSeconds()
                : null,
            DueOn = MilestoneDueDate.HasValue
                ? new DateTimeOffset(MilestoneDueDate.Value, TimeSpan.Zero).ToUnixTimeSeconds()
                : null
        };
    }

    /// <summary>
    /// Loads the project and milestones list.
    /// </summary>
    private async Task LoadPageDataAsync()
    {
        try
        {
            Project = await _testRail.GetProjectAsync(ProjectId);
            Milestones = (await _testRail.GetMilestonesAsync(ProjectId)).OrderBy(m => m.Name).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage ??= $"Failed to load milestones: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        Permissions = await _permissionService.GetPermissionsAsync();
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
    }
}
