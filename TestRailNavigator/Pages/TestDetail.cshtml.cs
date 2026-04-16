using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Models;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>
/// Page model for the TestDetail page displaying full details of an individual test.
/// </summary>
public class TestDetailModel : PageModel
{
    private readonly TestRailClient _testRail;
    private readonly SettingsService _settingsService;
    private readonly ConsoleLogService _consoleLog;
    private readonly PermissionService _permissionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestDetailModel"/> class.
    /// </summary>
    /// <param name="testRail">The TestRail client service.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="consoleLog">The console log service.</param>
    /// <param name="permissionService">The permission service.</param>
    public TestDetailModel(TestRailClient testRail, SettingsService settingsService, ConsoleLogService consoleLog, PermissionService permissionService)
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
    /// Gets or sets the test being viewed.
    /// </summary>
    public Test? Test { get; set; }

    /// <summary>
    /// Gets or sets the test run this test belongs to.
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
    /// Gets or sets the test plan if the run belongs to one.
    /// </summary>
    public TestPlan? Plan { get; set; }

    /// <summary>
    /// Gets or sets the sibling tests in the same run (for navigation).
    /// </summary>
    public List<Test> SiblingTests { get; set; } = [];

    /// <summary>
    /// Gets or sets the full result history for the test.
    /// </summary>
    public List<TestResult> Results { get; set; } = [];

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
    /// Handles GET requests to load the full details of a test.
    /// </summary>
    /// <param name="testId">The test identifier.</param>
    /// <returns>The page result or redirect to setup.</returns>
    public async Task<IActionResult> OnGetAsync(int testId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        try
        {
            Test = await _testRail.GetTestAsync(testId);

            if (Test is null)
            {
                ErrorMessage = $"Test {testId} not found.";
                return Page();
            }

            Run = await _testRail.GetRunAsync(Test.RunId);
            Results = await _testRail.GetResultsAsync(testId);
            SiblingTests = (await _testRail.GetTestsAsync(Test.RunId)).OrderBy(t => t.Title).ToList();

            if (Run is not null)
            {
                if (Run.PlanId is > 0)
                {
                    Plan = await _testRail.GetPlanAsync(Run.PlanId.Value);
                }

                if (Run.ProjectId > 0)
                {
                    Project = await _testRail.GetProjectAsync(Run.ProjectId);
                }

                if (Run.MilestoneId is > 0)
                {
                    Milestone = await _testRail.GetMilestoneAsync(Run.MilestoneId.Value);

                    if (Milestone?.ParentId.HasValue == true)
                    {
                        ParentMilestone = await _testRail.GetMilestoneAsync(Milestone.ParentId.Value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load test: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        Permissions = await _permissionService.GetPermissionsAsync();
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
        return Page();
    }

    /// <summary>
    /// Handles POST requests to quickly add a result for this test (Quick Edit).
    /// </summary>
    /// <param name="testId">The test identifier (route parameter).</param>
    /// <returns>A redirect back to the TestDetail page.</returns>
    public async Task<IActionResult> OnPostQuickEditAsync(int testId)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(testId);
            return Page();
        }

        var statusId = int.TryParse(Request.Form["QuickEditStatusId"], out var sid) ? sid : 0;
        var comment = Request.Form["QuickEditComment"].ToString();

        if (statusId <= 0)
        {
            ErrorMessage = "A status is required.";
            _consoleLog.Log(ErrorMessage);
            await ReloadPageDataAsync(testId);
            return Page();
        }

        try
        {
            Test = await _testRail.GetTestAsync(testId);

            if (Test is null)
            {
                ErrorMessage = $"Test {testId} not found.";
                await ReloadPageDataAsync(testId);
                return Page();
            }

            var request = new AddResultRequest
            {
                StatusId = statusId,
                Comment = string.IsNullOrWhiteSpace(comment) ? null : comment
            };
            await _testRail.AddResultForCaseAsync(Test.RunId, Test.CaseId, request);

            var statusName = statusId switch
            {
                1 => "Passed",
                2 => "Blocked",
                4 => "Retest",
                5 => "Failed",
                _ => "Updated"
            };
            SuccessMessage = $"Test C{Test.CaseId} marked as {statusName}.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to update test result: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await ReloadPageDataAsync(testId);
        return Page();
    }

    /// <summary>
    /// Reloads all page data after a mutation.
    /// </summary>
    private async Task ReloadPageDataAsync(int testId)
    {
        try
        {
            Test = await _testRail.GetTestAsync(testId);

            if (Test is not null)
            {
                Run = await _testRail.GetRunAsync(Test.RunId);
                Results = await _testRail.GetResultsAsync(testId);
                SiblingTests = (await _testRail.GetTestsAsync(Test.RunId)).OrderBy(t => t.Title).ToList();

                if (Run is not null)
                {
                    if (Run.PlanId is > 0)
                    {
                        Plan = await _testRail.GetPlanAsync(Run.PlanId.Value);
                    }

                    if (Run.ProjectId > 0)
                    {
                        Project = await _testRail.GetProjectAsync(Run.ProjectId);
                    }

                    if (Run.MilestoneId is > 0)
                    {
                        Milestone = await _testRail.GetMilestoneAsync(Run.MilestoneId.Value);

                        if (Milestone?.ParentId.HasValue == true)
                        {
                            ParentMilestone = await _testRail.GetMilestoneAsync(Milestone.ParentId.Value);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _consoleLog.Log($"Failed to reload page data: {ex.Message}");
        }

        Permissions = await _permissionService.GetPermissionsAsync();
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
    }
}
