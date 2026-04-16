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
    /// Gets or sets the open (active/upcoming) parent milestones for the sidebar.
    /// </summary>
    public List<Milestone> OpenMilestones { get; set; } = [];

    /// <summary>
    /// Gets or sets the closed (completed) parent milestones for the sidebar.
    /// </summary>
    public List<Milestone> ClosedMilestones { get; set; } = [];

    /// <summary>
    /// Gets or sets child milestones grouped by parent identifier for sidebar nesting.
    /// </summary>
    public Dictionary<int, List<Milestone>> MilestoneChildren { get; set; } = [];

    /// <summary>
    /// Gets or sets the child milestones for the selected parent milestone.
    /// </summary>
    public List<Milestone> SelectedChildMilestones { get; set; } = [];

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
    /// Gets or sets the parent of the selected milestone (for breadcrumb navigation).
    /// </summary>
    public Milestone? ParentMilestone { get; set; }

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
    /// Gets or sets a value indicating whether write operations are enabled.
    /// </summary>
    public bool WritesEnabled { get; set; }

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
    /// Gets or sets the run name for the create/edit form.
    /// </summary>
    [BindProperty]
    public string RunName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the run description for the create/edit form.
    /// </summary>
    [BindProperty]
    public string? RunDescription { get; set; }

    /// <summary>
    /// Gets or sets the run identifier for update/delete operations.
    /// </summary>
    [BindProperty]
    public int RunId { get; set; }

    /// <summary>
    /// Gets or sets the child milestone name for the create/edit form.
    /// </summary>
    [BindProperty]
    public string ChildMilestoneName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the child milestone description for the create/edit form.
    /// </summary>
    [BindProperty]
    public string? ChildMilestoneDescription { get; set; }

    /// <summary>
    /// Gets or sets the child milestone start date for the create/edit form.
    /// </summary>
    [BindProperty]
    public DateTime? ChildMilestoneStartDate { get; set; }

    /// <summary>
    /// Gets or sets the child milestone due date for the create/edit form.
    /// </summary>
    [BindProperty]
    public DateTime? ChildMilestoneDueDate { get; set; }

    /// <summary>
    /// Gets or sets the child milestone identifier for update/delete operations.
    /// </summary>
    [BindProperty]
    public int ChildMilestoneId { get; set; }

    /// <summary>
    /// Gets or sets whether the child milestone is completed (for update).
    /// </summary>
    [BindProperty]
    public bool ChildMilestoneIsCompleted { get; set; }

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
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();

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

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
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

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
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

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
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
    /// Handles POST requests to create a new standalone test run.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostCreateRunAsync(int projectId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        if (string.IsNullOrWhiteSpace(RunName))
        {
            ErrorMessage = "Run name is required.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        _consoleLog.Log($"Creating test run: {RunName}");

        try
        {
            var request = new CreateRunRequest
            {
                Name = RunName,
                Description = RunDescription,
                MilestoneId = MilestoneId,
                IncludeAll = false
            };
            var created = await _testRail.AddRunAsync(projectId, request);
            SuccessMessage = $"Test run '{created?.Name}' created successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create test run: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync(projectId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to update an existing test run's name and description.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostEditRunAsync(int projectId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        if (string.IsNullOrWhiteSpace(RunName))
        {
            ErrorMessage = "Run name is required.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        _consoleLog.Log($"Updating test run {RunId}: {RunName}");

        try
        {
            var request = new UpdateRunRequest
            {
                Name = RunName,
                Description = RunDescription
            };
            var updated = await _testRail.UpdateRunAsync(RunId, request);
            SuccessMessage = $"Test run '{updated?.Name}' updated successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to update test run: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync(projectId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to delete a test run.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostDeleteRunAsync(int projectId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        _consoleLog.Log($"Deleting test run {RunId}");

        try
        {
            await _testRail.DeleteRunAsync(RunId);
            SuccessMessage = "Test run deleted successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete test run: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync(projectId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to create a new child milestone under the selected milestone.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostCreateChildMilestoneAsync(int projectId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        if (string.IsNullOrWhiteSpace(ChildMilestoneName) || !MilestoneId.HasValue)
        {
            ErrorMessage = "Child milestone name and parent milestone are required.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        _consoleLog.Log($"Creating child milestone: {ChildMilestoneName} under milestone {MilestoneId}");

        try
        {
            var request = new MilestoneRequest
            {
                Name = ChildMilestoneName,
                Description = ChildMilestoneDescription,
                ParentId = MilestoneId.Value,
                StartOn = ChildMilestoneStartDate.HasValue
                    ? new DateTimeOffset(ChildMilestoneStartDate.Value, TimeSpan.Zero).ToUnixTimeSeconds()
                    : null,
                DueOn = ChildMilestoneDueDate.HasValue
                    ? new DateTimeOffset(ChildMilestoneDueDate.Value, TimeSpan.Zero).ToUnixTimeSeconds()
                    : null
            };
            var created = await _testRail.AddMilestoneAsync(projectId, request);
            SuccessMessage = $"Child milestone '{created?.Name}' created successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create child milestone: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync(projectId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to update a child milestone.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostEditChildMilestoneAsync(int projectId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        if (string.IsNullOrWhiteSpace(ChildMilestoneName))
        {
            ErrorMessage = "Child milestone name is required.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        _consoleLog.Log($"Updating child milestone {ChildMilestoneId}: {ChildMilestoneName}");

        try
        {
            var request = new MilestoneRequest
            {
                Name = ChildMilestoneName,
                Description = ChildMilestoneDescription,
                IsCompleted = ChildMilestoneIsCompleted,
                IsStarted = !ChildMilestoneIsCompleted && ChildMilestoneStartDate.HasValue && ChildMilestoneStartDate.Value <= DateTime.Today,
                StartOn = ChildMilestoneStartDate.HasValue
                    ? new DateTimeOffset(ChildMilestoneStartDate.Value, TimeSpan.Zero).ToUnixTimeSeconds()
                    : null,
                DueOn = ChildMilestoneDueDate.HasValue
                    ? new DateTimeOffset(ChildMilestoneDueDate.Value, TimeSpan.Zero).ToUnixTimeSeconds()
                    : null
            };
            var updated = await _testRail.UpdateMilestoneAsync(ChildMilestoneId, request);
            SuccessMessage = $"Child milestone '{updated?.Name}' updated successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to update child milestone: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync(projectId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to delete a child milestone.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostDeleteChildMilestoneAsync(int projectId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        _consoleLog.Log($"Deleting child milestone {ChildMilestoneId}");

        try
        {
            await _testRail.DeleteMilestoneAsync(ChildMilestoneId);
            SuccessMessage = "Child milestone deleted successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete child milestone: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync(projectId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to close (complete) a test plan. This action cannot be undone.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostClosePlanAsync(int projectId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        _consoleLog.Log($"Closing test plan {PlanId}");

        try
        {
            var closed = await _testRail.ClosePlanAsync(PlanId);
            SuccessMessage = $"Test plan '{closed?.Name}' closed successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to close test plan: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync(projectId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to close (complete) a test run. This action cannot be undone.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostCloseRunAsync(int projectId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await LoadPageDataAsync(projectId);
            return Page();
        }

        _consoleLog.Log($"Closing test run {RunId}");

        try
        {
            var closed = await _testRail.CloseRunAsync(RunId);
            SuccessMessage = $"Test run '{closed?.Name}' closed successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to close test run: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPageDataAsync(projectId);
        return Page();
    }

    /// <summary>
    /// Loads milestones, builds parent/child hierarchy, and splits parents into open and closed lists.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    private async Task LoadMilestonesAsync(int projectId)
    {
        try
        {
            var all = await _testRail.GetMilestonesAsync(projectId);

            // Build children lookup from flat list (child milestones have ParentId set)
            MilestoneChildren = all
                .Where(m => m.ParentId.HasValue)
                .OrderBy(m => m.Name)
                .GroupBy(m => m.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Also include children returned inline on parent milestones
            foreach (var parent in all.Where(m => m.Milestones is { Count: > 0 }))
            {
                if (!MilestoneChildren.ContainsKey(parent.Id))
                {
                    MilestoneChildren[parent.Id] = parent.Milestones!.OrderBy(m => m.Name).ToList();
                }
            }

            // Only show parent-level milestones in the sidebar
            var parents = all.Where(m => !m.ParentId.HasValue).ToList();
            OpenMilestones = parents.Where(m => !m.IsCompleted).OrderBy(m => m.Name).ToList();
            ClosedMilestones = parents.Where(m => m.IsCompleted).OrderBy(m => m.Name).ToList();
        }
        catch (Exception ex)
        {
            _consoleLog.Log($"Failed to load milestones: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads test plans and child milestones for the selected milestone.
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

            // Resolve parent milestone for breadcrumb when viewing a child milestone
            if (SelectedMilestone?.ParentId.HasValue == true)
            {
                ParentMilestone = OpenMilestones.Concat(ClosedMilestones)
                    .FirstOrDefault(m => m.Id == SelectedMilestone.ParentId.Value);

                if (ParentMilestone is null)
                {
                    ParentMilestone = await _testRail.GetMilestoneAsync(SelectedMilestone.ParentId.Value);
                }
            }

            MilestonePlans = await _testRail.GetPlansForMilestoneAsync(projectId, MilestoneId.Value);
            MilestoneRuns = await _testRail.GetRunsForMilestoneAsync(projectId, MilestoneId.Value);

            // Load child milestones for the right panel
            if (MilestoneChildren.TryGetValue(MilestoneId.Value, out var children))
            {
                SelectedChildMilestones = children;
            }
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
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
    }
}
