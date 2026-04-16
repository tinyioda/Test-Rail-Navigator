using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Models;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>
/// Page model for the TestCaseEdit page allowing full editing of a test case's properties.
/// </summary>
public class TestCaseEditModel : PageModel
{
    private readonly TestRailClient _testRail;
    private readonly SettingsService _settingsService;
    private readonly ConsoleLogService _consoleLog;
    private readonly PermissionService _permissionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestCaseEditModel"/> class.
    /// </summary>
    /// <param name="testRail">The TestRail client service.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="consoleLog">The console log service.</param>
    /// <param name="permissionService">The permission service.</param>
    public TestCaseEditModel(TestRailClient testRail, SettingsService settingsService, ConsoleLogService consoleLog, PermissionService permissionService)
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
    /// Gets or sets the test case being edited.
    /// </summary>
    public TestCase? TestCase { get; set; }

    /// <summary>
    /// Gets or sets the optional test instance that navigated here (for breadcrumb and return link).
    /// </summary>
    public Test? SourceTest { get; set; }

    /// <summary>
    /// Gets or sets the test run for breadcrumb navigation.
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
    /// Gets or sets the optional test identifier used for navigation context.
    /// </summary>
    public int? TestId { get; set; }

    /// <summary>
    /// Gets or sets the case title for form binding.
    /// </summary>
    [BindProperty]
    public string? CaseTitle { get; set; }

    /// <summary>
    /// Gets or sets the case type identifier for form binding.
    /// </summary>
    [BindProperty]
    public int? CaseTypeId { get; set; }

    /// <summary>
    /// Gets or sets the case priority identifier for form binding.
    /// </summary>
    [BindProperty]
    public int? CasePriorityId { get; set; }

    /// <summary>
    /// Gets or sets the case estimate for form binding.
    /// </summary>
    [BindProperty]
    public string? CaseEstimate { get; set; }

    /// <summary>
    /// Gets or sets the case references for form binding.
    /// </summary>
    [BindProperty]
    public string? CaseRefs { get; set; }

    /// <summary>
    /// Gets or sets the case preconditions for form binding.
    /// </summary>
    [BindProperty]
    public string? CasePreconditions { get; set; }

    /// <summary>
    /// Gets or sets the case steps for form binding.
    /// </summary>
    [BindProperty]
    public string? CaseSteps { get; set; }

    /// <summary>
    /// Gets or sets the case expected result for form binding.
    /// </summary>
    [BindProperty]
    public string? CaseExpectedResult { get; set; }

    /// <summary>
    /// Handles GET requests to load the test case for editing.
    /// </summary>
    /// <param name="caseId">The test case identifier.</param>
    /// <param name="testId">The optional test instance identifier for navigation context.</param>
    /// <returns>The page result or redirect to setup.</returns>
    public async Task<IActionResult> OnGetAsync(int caseId, int? testId = null)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        TestId = testId;

        try
        {
            TestCase = await _testRail.GetCaseAsync(caseId);

            if (TestCase is null)
            {
                ErrorMessage = $"Test case C{caseId} not found.";
                return Page();
            }

            CaseTitle = TestCase.Title;
            CaseTypeId = TestCase.TypeId;
            CasePriorityId = TestCase.PriorityId;
            CaseEstimate = TestCase.Estimate;
            CaseRefs = TestCase.Refs;
            CasePreconditions = TestCase.Preconditions;
            CaseSteps = TestCase.Steps;
            CaseExpectedResult = TestCase.ExpectedResult;

            await LoadNavigationContextAsync(testId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load test case: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        Permissions = await _permissionService.GetPermissionsAsync();
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
        return Page();
    }

    /// <summary>
    /// Handles POST requests to save changes to the test case.
    /// </summary>
    /// <param name="caseId">The test case identifier.</param>
    /// <param name="testId">The optional test instance identifier for navigation context.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnPostAsync(int caseId, int? testId = null)
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        TestId = testId;

        if (!await _settingsService.AreWritesEnabledAsync())
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log(ErrorMessage);
            await ReloadCaseAsync(caseId, testId);
            return Page();
        }

        if (string.IsNullOrWhiteSpace(CaseTitle))
        {
            ErrorMessage = "Title is required.";
            _consoleLog.Log(ErrorMessage);
            await ReloadCaseAsync(caseId, testId);
            return Page();
        }

        try
        {
            var request = new UpdateTestCaseRequest
            {
                Title = CaseTitle,
                TypeId = CaseTypeId,
                PriorityId = CasePriorityId,
                Estimate = string.IsNullOrWhiteSpace(CaseEstimate) ? null : CaseEstimate,
                Refs = string.IsNullOrWhiteSpace(CaseRefs) ? null : CaseRefs,
                Preconditions = string.IsNullOrWhiteSpace(CasePreconditions) ? null : CasePreconditions,
                Steps = string.IsNullOrWhiteSpace(CaseSteps) ? null : CaseSteps,
                ExpectedResult = string.IsNullOrWhiteSpace(CaseExpectedResult) ? null : CaseExpectedResult
            };

            var updated = await _testRail.UpdateCaseAsync(caseId, request);
            SuccessMessage = $"Test case C{caseId} '{updated?.Title}' updated successfully.";
            _consoleLog.Log(SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to update test case: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await ReloadCaseAsync(caseId, testId);
        return Page();
    }

    /// <summary>
    /// Loads breadcrumb navigation context from the source test instance.
    /// </summary>
    private async Task LoadNavigationContextAsync(int? testId)
    {
        if (testId is not > 0) return;

        try
        {
            SourceTest = await _testRail.GetTestAsync(testId.Value);

            if (SourceTest is not null)
            {
                Run = await _testRail.GetRunAsync(SourceTest.RunId);

                if (Run is not null)
                {
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
            _consoleLog.Log($"Failed to load navigation context: {ex.Message}");
        }
    }

    /// <summary>
    /// Reloads test case data after a mutation.
    /// </summary>
    private async Task ReloadCaseAsync(int caseId, int? testId)
    {
        try
        {
            TestCase = await _testRail.GetCaseAsync(caseId);

            if (TestCase is not null)
            {
                CaseTitle = TestCase.Title;
                CaseTypeId = TestCase.TypeId;
                CasePriorityId = TestCase.PriorityId;
                CaseEstimate = TestCase.Estimate;
                CaseRefs = TestCase.Refs;
                CasePreconditions = TestCase.Preconditions;
                CaseSteps = TestCase.Steps;
                CaseExpectedResult = TestCase.ExpectedResult;
            }

            await LoadNavigationContextAsync(testId);
        }
        catch (Exception ex)
        {
            _consoleLog.Log($"Failed to reload test case: {ex.Message}");
        }

        Permissions = await _permissionService.GetPermissionsAsync();
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
    }
}
