using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Models;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>
/// Page model for the Tests page displaying tests in a test run.
/// </summary>
public class TestsModel : PageModel
{
    private readonly TestRailClient _testRail;
    private readonly SettingsService _settingsService;
    private readonly ConsoleLogService _consoleLog;
    private readonly PermissionService _permissionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestsModel"/> class.
    /// </summary>
    /// <param name="testRail">The TestRail client service.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="consoleLog">The console log service.</param>
    /// <param name="permissionService">The permission service.</param>
    public TestsModel(TestRailClient testRail, SettingsService settingsService, ConsoleLogService consoleLog, PermissionService permissionService)
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
    /// Gets or sets the current test run.
    /// </summary>
    public TestRun? Run { get; set; }

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
    /// Gets or sets the list of tests to display.
    /// </summary>
    public List<Test> Tests { get; set; } = [];

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
    /// Gets or sets the sections available for the run's suite.
    /// </summary>
    public List<Section> Sections { get; set; } = [];

    /// <summary>
    /// Gets or sets the section identifier for the new test case.
    /// </summary>
    [BindProperty]
    public int CaseSectionId { get; set; }

    /// <summary>
    /// Gets or sets the title for the new test case.
    /// </summary>
    [BindProperty]
    public string? CaseTitle { get; set; }

    /// <summary>
    /// Gets or sets the type identifier for the new test case.
    /// </summary>
    [BindProperty]
    public int? CaseTypeId { get; set; }

    /// <summary>
    /// Gets or sets the priority identifier for the new test case.
    /// </summary>
    [BindProperty]
    public int? CasePriorityId { get; set; }

    /// <summary>
    /// Gets or sets the estimate for the new test case.
    /// </summary>
    [BindProperty]
    public string? CaseEstimate { get; set; }

    /// <summary>
    /// Gets or sets the milestone identifier for the new test case.
    /// </summary>
    [BindProperty]
    public int? CaseMilestoneId { get; set; }

    /// <summary>
    /// Gets or sets the references for the new test case.
    /// </summary>
    [BindProperty]
    public string? CaseRefs { get; set; }

    /// <summary>
    /// Gets or sets the preconditions for the new test case.
    /// </summary>
    [BindProperty]
    public string? CasePreconditions { get; set; }

    /// <summary>
    /// Gets or sets the steps for the new test case.
    /// </summary>
    [BindProperty]
    public string? CaseSteps { get; set; }

    /// <summary>
    /// Gets or sets the expected result for the new test case.
    /// </summary>
    [BindProperty]
    public string? CaseExpectedResult { get; set; }

    /// <summary>
    /// Gets or sets the available test runs in the project for the copy-tests picker.
    /// </summary>
    public List<TestRun> ProjectRuns { get; set; } = [];

    /// <summary>
    /// Gets or sets the source run identifier to copy tests from.
    /// </summary>
    [BindProperty]
    public int SourceRunId { get; set; }

    /// <summary>
    /// Gets or sets the available test cases from the project's case library.
    /// </summary>
    public List<TestCase> ExistingCases { get; set; } = [];

    /// <summary>
    /// Gets or sets the selected case identifiers to add to the run.
    /// </summary>
    [BindProperty]
    public List<int> SelectedCaseIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the case identifier for removal from the run.
    /// </summary>
    [BindProperty]
    public int DeleteCaseId { get; set; }

    /// <summary>
    /// Handles GET requests to load tests for a test run.
    /// </summary>
    /// <param name="runId">The test run identifier.</param>
    /// <returns>The page result or redirect to setup.</returns>
    public async Task<IActionResult> OnGetAsync(int runId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        try
        {
            Run = await _testRail.GetRunAsync(runId);
            ProjectId = Run?.ProjectId ?? 0;
            Tests = await _testRail.GetTestsAsync(runId);

            if (ProjectId > 0)
            {
                Project = await _testRail.GetProjectAsync(ProjectId);
            }

            if (Run?.SuiteId is > 0 && ProjectId > 0)
            {
                Sections = await _testRail.GetSectionsAsync(ProjectId, Run.SuiteId.Value);
            }

            if (Run?.MilestoneId is > 0)
            {
                Milestone = await _testRail.GetMilestoneAsync(Run.MilestoneId.Value);

                if (Milestone?.ParentId.HasValue == true)
                {
                    ParentMilestone = await _testRail.GetMilestoneAsync(Milestone.ParentId.Value);
                }

                var allPlans = await _testRail.GetPlansForMilestoneAsync(ProjectId, Run.MilestoneId.Value);
                OpenPlans = allPlans.Where(p => !p.IsCompleted).OrderBy(p => p.Name).ToList();
                ClosedPlans = allPlans.Where(p => p.IsCompleted).OrderBy(p => p.Name).ToList();

                var allRuns = await _testRail.GetRunsForMilestoneAsync(ProjectId, Run.MilestoneId.Value);
                OpenRuns = allRuns.Where(r => !r.IsCompleted).OrderBy(r => r.Name).ToList();
                ClosedRuns = allRuns.Where(r => r.IsCompleted).OrderBy(r => r.Name).ToList();
            }

            if (ProjectId > 0)
            {
                var allProjectRuns = await _testRail.GetRunsAsync(ProjectId);
                ProjectRuns = allProjectRuns.Where(r => r.Id != runId).OrderBy(r => r.Name).ToList();

                var existingCaseIds = Tests.Select(t => t.CaseId).ToHashSet();
                var allCases = await _testRail.GetCasesAsync(ProjectId, Run?.SuiteId);
                ExistingCases = allCases.Where(c => !existingCaseIds.Contains(c.Id)).OrderBy(c => c.Title).ToList();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load tests: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        Permissions = await _permissionService.GetPermissionsAsync();
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
        return Page();
    }

    /// <summary>
    /// Handles POST requests to add a new test case to the run's suite.
    /// </summary>
    /// <param name="runId">The test run identifier (for reloading).</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostAddTestAsync(int runId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(runId);
            return Page();
        }

        if (CaseSectionId <= 0 || string.IsNullOrWhiteSpace(CaseTitle))
        {
            ErrorMessage = "Section and title are required to add a test case.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(runId);
            return Page();
        }

        _consoleLog.Log($"Adding test case '{CaseTitle}' to section {CaseSectionId}");

        try
        {
            var request = new AddTestCaseRequest
            {
                Title = CaseTitle,
                TypeId = CaseTypeId,
                PriorityId = CasePriorityId,
                Estimate = string.IsNullOrWhiteSpace(CaseEstimate) ? null : CaseEstimate,
                MilestoneId = CaseMilestoneId,
                Refs = string.IsNullOrWhiteSpace(CaseRefs) ? null : CaseRefs,
                Preconditions = string.IsNullOrWhiteSpace(CasePreconditions) ? null : CasePreconditions,
                Steps = string.IsNullOrWhiteSpace(CaseSteps) ? null : CaseSteps,
                ExpectedResult = string.IsNullOrWhiteSpace(CaseExpectedResult) ? null : CaseExpectedResult
            };
            var testCase = await _testRail.AddCaseAsync(CaseSectionId, request);

            if (testCase is not null)
            {
                var currentTests = await _testRail.GetTestsAsync(runId);
                var mergedCaseIds = currentTests.Select(t => t.CaseId).Append(testCase.Id).Distinct().ToList();
                var updateRequest = new UpdateRunRequest { IncludeAll = false, CaseIds = mergedCaseIds };

                var targetRun = await _testRail.GetRunAsync(runId);
                if (targetRun?.PlanId is > 0)
                {
                    var plan = await _testRail.GetPlanAsync(targetRun.PlanId.Value);
                    var entry = plan?.Entries?.FirstOrDefault(e => e.Runs.Any(r => r.Id == runId));
                    if (entry is not null)
                    {
                        await _testRail.UpdatePlanEntryAsync(targetRun.PlanId.Value, entry.Id, updateRequest);
                    }
                }
                else
                {
                    await _testRail.UpdateRunAsync(runId, updateRequest);
                }
            }

            SuccessMessage = $"Test case '{testCase?.Title}' (C{testCase?.Id}) created and added to this run.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to add test case: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await ReloadPageDataAsync(runId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to copy test cases from a source run into the current run.
    /// </summary>
    /// <param name="runId">The target test run identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostCopyTestsAsync(int runId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(runId);
            return Page();
        }

        if (SourceRunId <= 0)
        {
            ErrorMessage = "Please select a source run to copy tests from.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(runId);
            return Page();
        }

        _consoleLog.Log($"Copying tests from run {SourceRunId} to run {runId}");

        try
        {
            var currentTests = await _testRail.GetTestsAsync(runId);
            var sourceTests = await _testRail.GetTestsAsync(SourceRunId);

            var currentCaseIds = currentTests.Select(t => t.CaseId).ToHashSet();
            var sourceCaseIds = sourceTests.Select(t => t.CaseId).ToList();
            var newCaseIds = sourceCaseIds.Where(id => !currentCaseIds.Contains(id)).ToList();

            if (newCaseIds.Count == 0)
            {
                SuccessMessage = "All tests from the source run already exist in this run. No changes made.";
                _consoleLog.Log(SuccessMessage);
                await ReloadPageDataAsync(runId);
                return Page();
            }

            var mergedCaseIds = currentCaseIds.Union(sourceCaseIds).ToList();
            var updateRequest = new UpdateRunRequest
            {
                IncludeAll = false,
                CaseIds = mergedCaseIds
            };

            var targetRun = await _testRail.GetRunAsync(runId);

            if (targetRun?.PlanId is > 0)
            {
                var plan = await _testRail.GetPlanAsync(targetRun.PlanId.Value);
                var entry = plan?.Entries?.FirstOrDefault(e => e.Runs.Any(r => r.Id == runId));

                if (entry is not null)
                {
                    await _testRail.UpdatePlanEntryAsync(targetRun.PlanId.Value, entry.Id, updateRequest);
                }
                else
                {
                    ErrorMessage = "Could not find the plan entry for this run.";
                    _consoleLog.Log(ErrorMessage);
                    await ReloadPageDataAsync(runId);
                    return Page();
                }
            }
            else
            {
                await _testRail.UpdateRunAsync(runId, updateRequest);
            }

            SuccessMessage = $"Copied {newCaseIds.Count} test(s) from the source run.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to copy tests: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await ReloadPageDataAsync(runId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to add existing test cases from the library to the current run.
    /// </summary>
    /// <param name="runId">The target test run identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostAddExistingAsync(int runId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(runId);
            return Page();
        }

        if (SelectedCaseIds.Count == 0)
        {
            ErrorMessage = "Please select at least one test case to add.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(runId);
            return Page();
        }

        _consoleLog.Log($"Adding {SelectedCaseIds.Count} existing case(s) to run {runId}");

        try
        {
            var currentTests = await _testRail.GetTestsAsync(runId);
            var currentCaseIds = currentTests.Select(t => t.CaseId).ToHashSet();
            var mergedCaseIds = currentCaseIds.Union(SelectedCaseIds).ToList();

            var updateRequest = new UpdateRunRequest
            {
                IncludeAll = false,
                CaseIds = mergedCaseIds
            };

            var targetRun = await _testRail.GetRunAsync(runId);

            if (targetRun?.PlanId is > 0)
            {
                var plan = await _testRail.GetPlanAsync(targetRun.PlanId.Value);
                var entry = plan?.Entries?.FirstOrDefault(e => e.Runs.Any(r => r.Id == runId));

                if (entry is not null)
                {
                    await _testRail.UpdatePlanEntryAsync(targetRun.PlanId.Value, entry.Id, updateRequest);
                }
                else
                {
                    ErrorMessage = "Could not find the plan entry for this run.";
                    _consoleLog.Log(ErrorMessage);
                    await ReloadPageDataAsync(runId);
                    return Page();
                }
            }
            else
            {
                await _testRail.UpdateRunAsync(runId, updateRequest);
            }

            var added = SelectedCaseIds.Count(id => !currentCaseIds.Contains(id));
            SuccessMessage = $"Added {added} test case(s) to this run.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to add existing tests: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await ReloadPageDataAsync(runId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to close (complete) the current test run. This action cannot be undone.
    /// </summary>
    /// <param name="runId">The test run identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostCloseRunAsync(int runId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(runId);
            return Page();
        }

        _consoleLog.Log($"Closing test run {runId}");

        try
        {
            var closed = await _testRail.CloseRunAsync(runId);
            SuccessMessage = $"Test run '{closed?.Name}' closed successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to close test run: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await ReloadPageDataAsync(runId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to remove a test case from the current run (does not delete the case from the library).
    /// </summary>
    /// <param name="runId">The test run identifier.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostRemoveTestAsync(int runId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(runId);
            return Page();
        }

        if (DeleteCaseId <= 0)
        {
            ErrorMessage = "Case identifier is required.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(runId);
            return Page();
        }

        _consoleLog.Log($"Removing case C{DeleteCaseId} from run {runId}");

        try
        {
            var currentTests = await _testRail.GetTestsAsync(runId);
            var remainingCaseIds = currentTests
                .Where(t => t.CaseId != DeleteCaseId)
                .Select(t => t.CaseId)
                .Distinct()
                .ToList();

            var updateRequest = new UpdateRunRequest { IncludeAll = false, CaseIds = remainingCaseIds };

            var targetRun = await _testRail.GetRunAsync(runId);
            if (targetRun?.PlanId is > 0)
            {
                var plan = await _testRail.GetPlanAsync(targetRun.PlanId.Value);
                var entry = plan?.Entries?.FirstOrDefault(e => e.Runs.Any(r => r.Id == runId));
                if (entry is not null)
                {
                    await _testRail.UpdatePlanEntryAsync(targetRun.PlanId.Value, entry.Id, updateRequest);
                }
            }
            else
            {
                await _testRail.UpdateRunAsync(runId, updateRequest);
            }

            SuccessMessage = $"Test case C{DeleteCaseId} removed from this run.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to remove test: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await ReloadPageDataAsync(runId);
        return Page();
    }

    /// <summary>
    /// Reloads all page data after a mutation.
    /// </summary>
    private async Task ReloadPageDataAsync(int runId)
    {
        try
        {
            Run = await _testRail.GetRunAsync(runId);
            ProjectId = Run?.ProjectId ?? 0;
            Tests = await _testRail.GetTestsAsync(runId);

            if (ProjectId > 0)
            {
                Project = await _testRail.GetProjectAsync(ProjectId);
            }

            if (Run?.SuiteId is > 0 && ProjectId > 0)
            {
                Sections = await _testRail.GetSectionsAsync(ProjectId, Run.SuiteId.Value);
            }

            if (Run?.MilestoneId is > 0)
            {
                Milestone = await _testRail.GetMilestoneAsync(Run.MilestoneId.Value);

                if (Milestone?.ParentId.HasValue == true)
                {
                    ParentMilestone = await _testRail.GetMilestoneAsync(Milestone.ParentId.Value);
                }

                var allPlans = await _testRail.GetPlansForMilestoneAsync(ProjectId, Run.MilestoneId.Value);
                OpenPlans = allPlans.Where(p => !p.IsCompleted).OrderBy(p => p.Name).ToList();
                ClosedPlans = allPlans.Where(p => p.IsCompleted).OrderBy(p => p.Name).ToList();

                var allRuns = await _testRail.GetRunsForMilestoneAsync(ProjectId, Run.MilestoneId.Value);
                OpenRuns = allRuns.Where(r => !r.IsCompleted).OrderBy(r => r.Name).ToList();
                ClosedRuns = allRuns.Where(r => r.IsCompleted).OrderBy(r => r.Name).ToList();
            }

            if (ProjectId > 0)
            {
                var allProjectRuns = await _testRail.GetRunsAsync(ProjectId);
                ProjectRuns = allProjectRuns.Where(r => r.Id != runId).OrderBy(r => r.Name).ToList();

                var existingCaseIds = Tests.Select(t => t.CaseId).ToHashSet();
                var allCases = await _testRail.GetCasesAsync(ProjectId, Run?.SuiteId);
                ExistingCases = allCases.Where(c => !existingCaseIds.Contains(c.Id)).OrderBy(c => c.Title).ToList();
            }
        }
        catch (Exception ex)
        {
            _consoleLog.Log($"Failed to reload page data: {ex.Message}");
        }

        Permissions = await _permissionService.GetPermissionsAsync();
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
    }

    /// <summary>
    /// Handles AJAX GET requests to fetch the latest results for a specific test.
    /// </summary>
    /// <param name="runId">The run identifier (route parameter).</param>
    /// <param name="testId">The test identifier.</param>
    /// <returns>A JSON array of test results.</returns>
    public async Task<IActionResult> OnGetResultsAsync(int runId, int testId)
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
    /// <param name="runId">The test run identifier (route parameter).</param>
    /// <returns>A redirect back to the Tests page.</returns>
    public async Task<IActionResult> OnPostQuickEditAsync(int runId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(runId);
            return Page();
        }

        var caseId = int.TryParse(Request.Form["QuickEditCaseId"], out var cid) ? cid : 0;
        var statusId = int.TryParse(Request.Form["QuickEditStatusId"], out var sid) ? sid : 0;
        var comment = Request.Form["QuickEditComment"].ToString();

        if (caseId <= 0 || statusId <= 0)
        {
            ErrorMessage = "A test case and status are required.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(runId);
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

        await ReloadPageDataAsync(runId);
        return Page();
    }

    /// <summary>
    /// Handles POST requests to quickly edit a test case's core properties from the grid.
    /// </summary>
    /// <param name="runId">The test run identifier (route parameter).</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostQuickCaseEditAsync(int runId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(runId);
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
            await ReloadPageDataAsync(runId);
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

        await ReloadPageDataAsync(runId);
        return Page();
    }
}
