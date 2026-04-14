using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Models;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>
/// Page model for the Project detail hub displaying navigation to all project areas.
/// </summary>
public class ProjectModel : PageModel
{
    private readonly TestRailClient _testRail;
    private readonly SettingsService _settingsService;
    private readonly ConsoleLogService _consoleLog;
    private readonly PermissionService _permissionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectModel"/> class.
    /// </summary>
    /// <param name="testRail">The TestRail client service.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="consoleLog">The console log service.</param>
    /// <param name="permissionService">The permission service.</param>
    public ProjectModel(TestRailClient testRail, SettingsService settingsService, ConsoleLogService consoleLog, PermissionService permissionService)
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
    /// Gets or sets the open (active/upcoming) milestones for the sidebar.
    /// </summary>
    public List<Milestone> OpenMilestones { get; set; } = [];

    /// <summary>
    /// Gets or sets the closed (completed) milestones for the sidebar.
    /// </summary>
    public List<Milestone> ClosedMilestones { get; set; } = [];

    /// <summary>
    /// Gets or sets the selected milestone identifier from the query string.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int? MilestoneId { get; set; }

    /// <summary>
    /// Gets or sets the selected milestone.
    /// </summary>
    public Milestone? SelectedMilestone { get; set; }

    /// <summary>
    /// Gets or sets the test plans for the selected milestone.
    /// </summary>
    public List<TestPlan> MilestonePlans { get; set; } = [];

    /// <summary>
    /// Gets or sets the test runs for the selected milestone.
    /// </summary>
    public List<TestRun> MilestoneRuns { get; set; } = [];

    /// <summary>
    /// Gets or sets the error message if loading the project itself fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the success message after an action.
    /// </summary>
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Gets or sets the plan name for the create/edit form.
    /// </summary>
    [BindProperty]
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plan description for the create/edit form.
    /// </summary>
    [BindProperty]
    public string? PlanDescription { get; set; }

    /// <summary>
    /// Gets or sets the plan identifier for update/delete operations.
    /// </summary>
    [BindProperty]
    public int PlanId { get; set; }

    /// <summary>
    /// Handles GET requests to load the project overview.
    /// Each section is loaded independently so a single API failure does not break the entire page.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The page result or redirect to setup.</returns>
    public async Task<IActionResult> OnGetAsync(int projectId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        try
        {
            Project = await _testRail.GetProjectAsync(projectId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load project: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
            return Page();
        }

        await LoadMilestonesAsync(projectId);
        await LoadSelectedMilestonePlansAsync(projectId);
        Permissions = await _permissionService.GetPermissionsAsync();

        return Page();
    }

    /// <summary>
    /// Handles POST requests to create a new test plan.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostCreatePlanAsync(int projectId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (string.IsNullOrWhiteSpace(PlanName))
        {
            ErrorMessage = "Plan name is required.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        _consoleLog.Log($"Creating test plan: {PlanName}");

        try
        {
            var request = new TestPlanRequest
            {
                Name = PlanName,
                Description = PlanDescription,
                MilestoneId = MilestoneId
            };
            var created = await _testRail.AddPlanAsync(projectId, request);
            SuccessMessage = $"Test plan '{created?.Name}' created successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create test plan: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync(projectId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to update an existing test plan.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostEditPlanAsync(int projectId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (string.IsNullOrWhiteSpace(PlanName))
        {
            ErrorMessage = "Plan name is required.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        _consoleLog.Log($"Updating test plan {PlanId}: {PlanName}");

        try
        {
            var request = new TestPlanRequest
            {
                Name = PlanName,
                Description = PlanDescription,
                MilestoneId = MilestoneId
            };
            var updated = await _testRail.UpdatePlanAsync(PlanId, request);
            SuccessMessage = $"Test plan '{updated?.Name}' updated successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to update test plan: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync(projectId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to delete a test plan.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostDeletePlanAsync(int projectId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        _consoleLog.Log($"Deleting test plan {PlanId}");

        try
        {
            await _testRail.DeletePlanAsync(PlanId);
            SuccessMessage = "Test plan deleted successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete test plan: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync(projectId);
        return Page();
    }

    /// <summary>
    /// Loads milestones and splits them into open and closed lists.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    private async Task LoadMilestonesAsync(int projectId)
    {
        try
        {
            var all = await _testRail.GetMilestonesAsync(projectId);
            OpenMilestones = all.Where(m => !m.IsCompleted).ToList();
            ClosedMilestones = all.Where(m => m.IsCompleted).ToList();
        }
        catch (Exception ex)
        {
            _consoleLog.Log($"Failed to load milestones: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads test plans for the selected milestone.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    private async Task LoadSelectedMilestonePlansAsync(int projectId)
    {
        if (!MilestoneId.HasValue)
        {
            return;
        }

        try
        {
            SelectedMilestone = OpenMilestones.Concat(ClosedMilestones)
                .FirstOrDefault(m => m.Id == MilestoneId.Value);

            if (SelectedMilestone is null)
            {
                SelectedMilestone = await _testRail.GetMilestoneAsync(MilestoneId.Value);
            }

            MilestonePlans = await _testRail.GetPlansForMilestoneAsync(projectId, MilestoneId.Value);
            MilestoneRuns = await _testRail.GetRunsForMilestoneAsync(projectId, MilestoneId.Value);
        }
        catch (Exception ex)
        {
            _consoleLog.Log($"Failed to load data for milestone: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads all page data (project, milestones, selected milestone plans).
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    private async Task LoadPageDataAsync(int projectId)
    {
        try
        {
            Project = await _testRail.GetProjectAsync(projectId);
        }
        catch (Exception ex)
        {
            ErrorMessage ??= $"Failed to load project: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadMilestonesAsync(projectId);
        await LoadSelectedMilestonePlansAsync(projectId);
        Permissions = await _permissionService.GetPermissionsAsync();
    }
}
