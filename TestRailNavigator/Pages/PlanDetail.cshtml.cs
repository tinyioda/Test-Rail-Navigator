using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Models;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>
/// Page model for the PlanDetail page displaying entries and runs within a test plan.
/// </summary>
public class PlanDetailModel : PageModel
{
    private readonly TestRailClient _testRail;
    private readonly SettingsService _settingsService;
    private readonly ConsoleLogService _consoleLog;
    private readonly PermissionService _permissionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanDetailModel"/> class.
    /// </summary>
    /// <param name="testRail">The TestRail client service.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="consoleLog">The console log service.</param>
    /// <param name="permissionService">The permission service.</param>
    public PlanDetailModel(TestRailClient testRail, SettingsService settingsService, ConsoleLogService consoleLog, PermissionService permissionService)
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
    /// Gets or sets the test plan with entries and runs.
    /// </summary>
    public TestPlan? Plan { get; set; }

    /// <summary>
    /// Gets or sets the project for breadcrumb navigation.
    /// </summary>
    public Project? Project { get; set; }

    /// <summary>
    /// Gets or sets the milestone for breadcrumb navigation.
    /// </summary>
    public Milestone? Milestone { get; set; }

    /// <summary>
    /// Gets or sets the project identifier for breadcrumb navigation.
    /// </summary>
    public int ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the open test plans in the same milestone.
    /// </summary>
    public List<TestPlan> OpenPlans { get; set; } = [];

    /// <summary>
    /// Gets or sets the closed test plans in the same milestone.
    /// </summary>
    public List<TestPlan> ClosedPlans { get; set; } = [];

    /// <summary>
    /// Gets or sets the open test runs in the same milestone.
    /// </summary>
    public List<TestRun> OpenRuns { get; set; } = [];

    /// <summary>
    /// Gets or sets the closed test runs in the same milestone.
    /// </summary>
    public List<TestRun> ClosedRuns { get; set; } = [];

    /// <summary>
    /// Gets or sets the error message if loading fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the success message after a mutation.
    /// </summary>
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Gets or sets the tests for each run, keyed by run identifier.
    /// </summary>
    public Dictionary<int, List<Test>> RunTests { get; set; } = [];

    /// <summary>
    /// Gets or sets the available suites for creating new entries.
    /// </summary>
    public List<Suite> Suites { get; set; } = [];

    /// <summary>
    /// Gets or sets the entry name from the form.
    /// </summary>
    [BindProperty]
    public string? EntryName { get; set; }

    /// <summary>
    /// Gets or sets the suite identifier from the form.
    /// </summary>
    [BindProperty]
    public int EntrySuiteId { get; set; }

    /// <summary>
    /// Gets or sets the entry identifier for rename operations.
    /// </summary>
    [BindProperty]
    public string? RenameEntryId { get; set; }

    /// <summary>
    /// Gets or sets the new name for rename operations.
    /// </summary>
    [BindProperty]
    public string? RenameEntryName { get; set; }

    /// <summary>
    /// Gets or sets the entry identifier for delete operations.
    /// </summary>
    [BindProperty]
    public string? DeleteEntryId { get; set; }

    /// <summary>
    /// Handles GET requests to load a test plan with its entries and runs.
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    /// <returns>The page result or redirect to setup.</returns>
    public async Task<IActionResult> OnGetAsync(int planId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        try
        {
            Plan = await _testRail.GetPlanAsync(planId);
            ProjectId = Plan?.ProjectId ?? 0;

            if (ProjectId > 0)
            {
                Project = await _testRail.GetProjectAsync(ProjectId);
                Suites = await _testRail.GetSuitesAsync(ProjectId);
            }

            if (Plan?.MilestoneId is > 0 && ProjectId > 0)
            {
                Milestone = await _testRail.GetMilestoneAsync(Plan.MilestoneId.Value);

                var allPlans = await _testRail.GetPlansForMilestoneAsync(ProjectId, Plan.MilestoneId.Value);
                OpenPlans = allPlans.Where(p => !p.IsCompleted).ToList();
                ClosedPlans = allPlans.Where(p => p.IsCompleted).ToList();

                var allRuns = await _testRail.GetRunsForMilestoneAsync(ProjectId, Plan.MilestoneId.Value);
                OpenRuns = allRuns.Where(r => !r.IsCompleted).ToList();
                ClosedRuns = allRuns.Where(r => r.IsCompleted).ToList();
            }

            if (Plan?.Entries is not null)
            {
                foreach (var entry in Plan.Entries)
                {
                    foreach (var run in entry.Runs)
                    {
                        try
                        {
                            var tests = await _testRail.GetTestsAsync(run.Id);
                            RunTests[run.Id] = tests;
                        }
                        catch (Exception ex)
                        {
                            _consoleLog.Log($"Failed to load tests for run {run.Id}: {ex.Message}");
                            RunTests[run.Id] = [];
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load test plan: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        Permissions = await _permissionService.GetPermissionsAsync();
        return Page();
    }

    /// <summary>
    /// Handles POST requests to add a new entry to the test plan.
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostAddEntryAsync(int planId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (EntrySuiteId <= 0)
        {
            ErrorMessage = "A test run could not be created. Please try again.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(planId);
            return Page();
        }

        _consoleLog.Log($"Adding test run to plan {planId}");

        try
        {
            var request = new TestPlanEntryRequest
            {
                SuiteId = EntrySuiteId,
                Name = string.IsNullOrWhiteSpace(EntryName) ? null : EntryName,
                IncludeAll = false,
                CaseIds = []
            };
            var entry = await _testRail.AddPlanEntryAsync(planId, request);
            SuccessMessage = $"Test run '{entry?.Name}' added successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to add test run: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await ReloadPageDataAsync(planId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to rename a test run entry within the plan.
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostRenameEntryAsync(int planId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (string.IsNullOrWhiteSpace(RenameEntryId) || string.IsNullOrWhiteSpace(RenameEntryName))
        {
            ErrorMessage = "Entry identifier and new name are required.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(planId);
            return Page();
        }

        _consoleLog.Log($"Renaming entry {RenameEntryId} in plan {planId} to '{RenameEntryName}'");

        try
        {
            var entry = await _testRail.RenamePlanEntryAsync(planId, RenameEntryId, RenameEntryName);
            SuccessMessage = $"Test run renamed to '{entry?.Name}' successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to rename test run: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await ReloadPageDataAsync(planId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to delete a test run entry from the plan.
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostDeleteEntryAsync(int planId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (string.IsNullOrWhiteSpace(DeleteEntryId))
        {
            ErrorMessage = "Entry identifier is required.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(planId);
            return Page();
        }

        _consoleLog.Log($"Deleting entry {DeleteEntryId} from plan {planId}");

        try
        {
            await _testRail.DeletePlanEntryAsync(planId, DeleteEntryId);
            SuccessMessage = "Test run deleted successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete test run: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await ReloadPageDataAsync(planId);
        return Page();
    }

    /// <summary>
    /// Reloads all page data after a mutation.
    /// </summary>
    private async Task ReloadPageDataAsync(int planId)
    {
        try
        {
            Plan = await _testRail.GetPlanAsync(planId);
            ProjectId = Plan?.ProjectId ?? 0;

            if (ProjectId > 0)
            {
                Project = await _testRail.GetProjectAsync(ProjectId);
                Suites = await _testRail.GetSuitesAsync(ProjectId);
            }

            if (Plan?.MilestoneId is > 0 && ProjectId > 0)
            {
                Milestone = await _testRail.GetMilestoneAsync(Plan.MilestoneId.Value);

                var allPlans = await _testRail.GetPlansForMilestoneAsync(ProjectId, Plan.MilestoneId.Value);
                OpenPlans = allPlans.Where(p => !p.IsCompleted).ToList();
                ClosedPlans = allPlans.Where(p => p.IsCompleted).ToList();

                var allRuns = await _testRail.GetRunsForMilestoneAsync(ProjectId, Plan.MilestoneId.Value);
                OpenRuns = allRuns.Where(r => !r.IsCompleted).ToList();
                ClosedRuns = allRuns.Where(r => r.IsCompleted).ToList();
            }

            if (Plan?.Entries is not null)
            {
                foreach (var entry in Plan.Entries)
                {
                    foreach (var run in entry.Runs)
                    {
                        try
                        {
                            var tests = await _testRail.GetTestsAsync(run.Id);
                            RunTests[run.Id] = tests;
                        }
                        catch (Exception ex)
                        {
                            _consoleLog.Log($"Failed to load tests for run {run.Id}: {ex.Message}");
                            RunTests[run.Id] = [];
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to reload page data: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        Permissions = await _permissionService.GetPermissionsAsync();
    }
}
