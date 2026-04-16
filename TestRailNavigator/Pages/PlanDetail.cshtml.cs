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
    /// Gets or sets the parent of the milestone (for breadcrumb navigation).
    /// </summary>
    public Milestone? ParentMilestone { get; set; }

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
    /// Gets or sets a value indicating whether write operations are enabled.
    /// </summary>
    public bool WritesEnabled { get; set; }

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
    /// Gets or sets the case identifier for removal from a run.
    /// </summary>
    [BindProperty]
    public int DeleteCaseId { get; set; }

    /// <summary>
    /// Gets or sets the run identifier for the remove-test operation.
    /// </summary>
    [BindProperty]
    public int DeleteRunId { get; set; }

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

                if (Milestone?.ParentId.HasValue == true)
                {
                    ParentMilestone = await _testRail.GetMilestoneAsync(Milestone.ParentId.Value);
                }

                var allPlans = await _testRail.GetPlansForMilestoneAsync(ProjectId, Plan.MilestoneId.Value);
                OpenPlans = allPlans.Where(p => !p.IsCompleted).OrderBy(p => p.Name).ToList();
                ClosedPlans = allPlans.Where(p => p.IsCompleted).OrderBy(p => p.Name).ToList();

                var allRuns = await _testRail.GetRunsForMilestoneAsync(ProjectId, Plan.MilestoneId.Value);
                OpenRuns = allRuns.Where(r => !r.IsCompleted).OrderBy(r => r.Name).ToList();
                ClosedRuns = allRuns.Where(r => r.IsCompleted).OrderBy(r => r.Name).ToList();
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
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
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

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(planId);
            return Page();
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

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(planId);
            return Page();
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

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(planId);
            return Page();
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
    /// Handles POST requests to close (complete) the test plan. This action cannot be undone.
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostClosePlanAsync(int planId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(planId);
            return Page();
        }

        _consoleLog.Log($"Closing test plan {planId}");

        try
        {
            var closed = await _testRail.ClosePlanAsync(planId);
            SuccessMessage = $"Test plan '{closed?.Name}' closed successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to close test plan: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await ReloadPageDataAsync(planId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to remove a test case from a run within the plan (does not delete the case from the library).
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostRemoveTestAsync(int planId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(planId);
            return Page();
        }

        if (DeleteCaseId <= 0 || DeleteRunId <= 0)
        {
            ErrorMessage = "Case and run identifiers are required.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(planId);
            return Page();
        }

        _consoleLog.Log($"Removing case C{DeleteCaseId} from run {DeleteRunId} in plan {planId}");

        try
        {
            var currentTests = await _testRail.GetTestsAsync(DeleteRunId);
            var remainingCaseIds = currentTests
                .Where(t => t.CaseId != DeleteCaseId)
                .Select(t => t.CaseId)
                .Distinct()
                .ToList();

            var plan = await _testRail.GetPlanAsync(planId);
            var entry = plan?.Entries?.FirstOrDefault(e => e.Runs.Any(r => r.Id == DeleteRunId));
            if (entry is not null)
            {
                var updateRequest = new UpdateRunRequest { IncludeAll = false, CaseIds = remainingCaseIds };
                await _testRail.UpdatePlanEntryAsync(planId, entry.Id, updateRequest);
            }

            SuccessMessage = $"Test case C{DeleteCaseId} removed from run.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to remove test: {ex.Message}";
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

                if (Milestone?.ParentId.HasValue == true)
                {
                    ParentMilestone = await _testRail.GetMilestoneAsync(Milestone.ParentId.Value);
                }

                var allPlans = await _testRail.GetPlansForMilestoneAsync(ProjectId, Plan.MilestoneId.Value);
                OpenPlans = allPlans.Where(p => !p.IsCompleted).OrderBy(p => p.Name).ToList();
                ClosedPlans = allPlans.Where(p => p.IsCompleted).OrderBy(p => p.Name).ToList();

                var allRuns = await _testRail.GetRunsForMilestoneAsync(ProjectId, Plan.MilestoneId.Value);
                OpenRuns = allRuns.Where(r => !r.IsCompleted).OrderBy(r => r.Name).ToList();
                ClosedRuns = allRuns.Where(r => r.IsCompleted).OrderBy(r => r.Name).ToList();
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
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
    }

    /// <summary>
    /// Handles AJAX GET requests to fetch the latest results for a specific test.
    /// </summary>
    /// <param name="planId">The plan identifier (route parameter).</param>
    /// <param name="testId">The test identifier.</param>
    /// <returns>A JSON array of test results.</returns>
    public async Task<IActionResult> OnGetResultsAsync(int planId, int testId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return new JsonResult(Array.Empty<object>());
        }

        try
        {
            var results = await _testRail.GetResultsAsync(testId);
            var data = results.Take(10).Select(r => new
            {
                r.Id,
                r.StatusId,
                r.StatusName,
                r.Comment,
                CreatedOn = DateTimeOffset.FromUnixTimeSeconds(r.CreatedOn).UtcDateTime.ToString("yyyy-MM-dd HH:mm UTC")
            });
            return new JsonResult(data);
        }
        catch (Exception ex)
        {
            _consoleLog.Log($"Failed to load results for test {testId}: {ex.Message}");
            return new JsonResult(Array.Empty<object>());
        }
    }

    /// <summary>
    /// Handles POST requests to quickly add a result for a test (Quick Edit).
    /// </summary>
    /// <param name="planId">The test plan identifier (route parameter).</param>
    /// <returns>A redirect back to the PlanDetail page.</returns>
    public async Task<IActionResult> OnPostQuickEditAsync(int planId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(planId);
            return Page();
        }

        var runId = int.TryParse(Request.Form["QuickEditRunId"], out var rid) ? rid : 0;
        var caseId = int.TryParse(Request.Form["QuickEditCaseId"], out var cid) ? cid : 0;
        var statusId = int.TryParse(Request.Form["QuickEditStatusId"], out var sid) ? sid : 0;
        var comment = Request.Form["QuickEditComment"].ToString();

        if (runId <= 0 || caseId <= 0 || statusId <= 0)
        {
            ErrorMessage = "A test run, case, and status are required.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(planId);
            return Page();
        }

        try
        {
            var request = new AddResultRequest
            {
                StatusId = statusId,
                Comment = string.IsNullOrWhiteSpace(comment) ? null : comment
            };
            await _testRail.AddResultForCaseAsync(runId, caseId, request);

            var statusName = statusId switch
            {
                1 => "Passed",
                2 => "Blocked",
                4 => "Retest",
                5 => "Failed",
                _ => "Updated"
            };
            SuccessMessage = $"Test C{caseId} marked as {statusName}.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to update test result: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await ReloadPageDataAsync(planId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to quickly edit a test case's core properties from the grid.
    /// </summary>
    /// <param name="planId">The test plan identifier (route parameter).</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostQuickCaseEditAsync(int planId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(planId);
            return Page();
        }

        var caseId = int.TryParse(Request.Form["EditCaseId"], out var cid) ? cid : 0;
        var title = Request.Form["EditCaseTitle"].ToString();
        var typeId = int.TryParse(Request.Form["EditCaseTypeId"], out var tid) ? tid : (int?)null;
        var priorityId = int.TryParse(Request.Form["EditCasePriorityId"], out var pid) ? pid : (int?)null;
        var estimate = Request.Form["EditCaseEstimate"].ToString();

        if (caseId <= 0 || string.IsNullOrWhiteSpace(title))
        {
            ErrorMessage = "Case ID and title are required.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(planId);
            return Page();
        }

        try
        {
            var request = new UpdateTestCaseRequest
            {
                Title = title,
                TypeId = typeId,
                PriorityId = priorityId,
                Estimate = string.IsNullOrWhiteSpace(estimate) ? null : estimate
            };
            var updated = await _testRail.UpdateCaseAsync(caseId, request);

            SuccessMessage = $"Test case C{caseId} '{updated?.Title}' updated.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to update test case: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await ReloadPageDataAsync(planId);
        return Page();
    }
}
