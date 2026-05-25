using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Models;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>
/// Wizard that takes a single Azure DevOps user story URL and, after explicit user confirmation,
/// creates a new TestRail plan with a single run containing test cases generated from the
/// story's acceptance criteria.
/// </summary>
/// <remarks>
/// Strict rules enforced:
/// <list type="bullet">
///   <item>No TestRail write happens until the user clicks the Confirm-modal's "Yes, create it" button.</item>
///   <item>The folder-structure destination is never invented — the parent section must already exist.</item>
///   <item><see cref="SettingsService.AreWritesEnabledAsync"/> is re-checked server-side before any write.</item>
/// </list>
/// </remarks>
public class CreatePlanFromStoryModel : PageModel
{
    private readonly TestRailClient _testRail;
    private readonly AzureDevOpsService _azureDevOps;
    private readonly SettingsService _settingsService;
    private readonly ConsoleLogService _consoleLog;
    private readonly PermissionService _permissionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePlanFromStoryModel"/> class.
    /// </summary>
    public CreatePlanFromStoryModel(
        TestRailClient testRail,
        AzureDevOpsService azureDevOps,
        SettingsService settingsService,
        ConsoleLogService consoleLog,
        PermissionService permissionService)
    {
        _testRail = testRail;
        _azureDevOps = azureDevOps;
        _settingsService = settingsService;
        _consoleLog = consoleLog;
        _permissionService = permissionService;
    }

    /// <summary>Gets or sets the round-tripped wizard payload (story, destination, plan/run details, case drafts).</summary>
    [BindProperty]
    public CreatePlanFromStoryRequest Wizard { get; set; } = new();

    /// <summary>Gets or sets the current user's TestRail permissions.</summary>
    public TestRailPermissions Permissions { get; set; } = TestRailPermissions.ReadOnly();

    /// <summary>Gets or sets a value indicating whether write operations are enabled.</summary>
    public bool WritesEnabled { get; set; }

    /// <summary>Gets a value indicating whether an AzDO PAT is configured.</summary>
    public bool AzureDevOpsConfigured { get; private set; }

    /// <summary>Gets or sets the list of projects for the project picker (alphabetical).</summary>
    public List<Project> Projects { get; set; } = [];

    /// <summary>Gets or sets the list of suites for the selected project (alphabetical).</summary>
    public List<Suite> Suites { get; set; } = [];

    /// <summary>Gets or sets the flat section list for the selected suite.</summary>
    public List<Section> Sections { get; set; } = [];

    /// <summary>Gets or sets the rooted section tree for the folder picker.</summary>
    public IReadOnlyList<SectionTreeNode> SectionTree { get; set; } = [];

    /// <summary>Gets or sets the resolved destination breadcrumb shown on the review step.</summary>
    public string? ResolvedDestinationBreadcrumb { get; set; }

    /// <summary>Gets or sets the path-resolver diagnostic message (missing/ambiguous segments) for display.</summary>
    public string? DestinationMessage { get; set; }

    /// <summary>Gets or sets a value indicating whether the Load step produced a valid preview.</summary>
    public bool PreviewReady { get; set; }

    /// <summary>Gets or sets the error banner message.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the success banner message.</summary>
    public string? SuccessMessage { get; set; }

    /// <summary>Gets or sets the ID of the plan created on Confirm (populated after successful commit).</summary>
    public int? CreatedPlanId { get; set; }

    /// <summary>
    /// GET handler — blank wizard.
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        await LoadContextAsync();
        return Page();
    }

    /// <summary>
    /// POST handler — fetches the story from AzDO, parses acceptance criteria into case drafts,
    /// and resolves the destination folder. Does not call TestRail write endpoints.
    /// </summary>
    public async Task<IActionResult> OnPostLoadAsync()
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        await LoadContextAsync();

        if (!AzureDevOpsConfigured)
        {
            ErrorMessage = "An Azure DevOps PAT must be configured on the Setup page before loading a story.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Wizard.StoryUrl))
        {
            ErrorMessage = "Enter an Azure DevOps work item URL.";
            return Page();
        }

        if (Wizard.ProjectId <= 0 || Wizard.SuiteId <= 0)
        {
            ErrorMessage = "Select a project and suite before loading the story.";
            return Page();
        }

        IssueTrackerItem? item;
        try
        {
            item = await _azureDevOps.GetItemAsync(Wizard.StoryUrl);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to fetch story: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
            return Page();
        }

        if (item is null)
        {
            ErrorMessage = "The URL did not resolve to a recognizable Azure DevOps work item.";
            return Page();
        }

        // Populate story display fields (authoritative values come from the URL on Confirm).
        Wizard.StoryId = item.NumericId;
        Wizard.StoryTitle = item.Title;
        Wizard.StoryType = item.RawType;

        // Parse acceptance criteria (falling back to repro steps for Bug-type items).
        var sourceHtml = item.AcceptanceCriteriaHtml;
        if (string.IsNullOrWhiteSpace(sourceHtml) && !string.IsNullOrWhiteSpace(item.ReproStepsHtml))
        {
            sourceHtml = item.ReproStepsHtml;
        }

        var fallbackTitle = string.IsNullOrWhiteSpace(item.Title)
            ? $"{item.RawType} {item.NumericId}".Trim()
            : item.Title;

        var parseResult = AcceptanceCriteriaParser.ParseWithDiagnostics(sourceHtml, fallbackTitle);
        Wizard.DetectedAcFormat = parseResult.Diagnostics.DetectedFormat;
        Wizard.DetectedAcItemCount = parseResult.Diagnostics.ItemCount;

        var precondsDefault = AzureDevOpsService.HtmlToPlainText(item.DescriptionHtml);
        Wizard.Cases = parseResult.Candidates.Select(c => new CaseDraft
        {
            Title = c.Title,
            Steps = c.Steps,
            Expected = c.Expected,
            Preconditions = precondsDefault,
            Include = true,
            Refs = item.NumericId is int n ? $"AB#{n}" : item.Reference,
            OriginalTitle = c.Title,
            OriginalSteps = c.Steps,
            OriginalExpected = c.Expected,
            OriginalPreconditions = precondsDefault
        }).ToList();

        // Default plan/run names if the user hasn't overridden them.
        if (string.IsNullOrWhiteSpace(Wizard.PlanName))
        {
            Wizard.PlanName = item.NumericId is int id
                ? $"[AB#{id}] {item.Title}"
                : $"{item.RawType}: {item.Title}";
        }

        if (string.IsNullOrWhiteSpace(Wizard.RunName))
        {
            Wizard.RunName = "Run 1";
        }

        ResolveDestination();
        PreviewReady = true;
        _consoleLog.Log($"Loaded story {item.Reference}: {parseResult.Diagnostics.ItemCount} case(s) from '{parseResult.Diagnostics.DetectedFormat}'.");
        return Page();
    }

    /// <summary>
    /// POST handler — commits the wizard payload. Creates section (if opted in), then cases, then plan,
    /// then plan entry. Re-checks <see cref="SettingsService.AreWritesEnabledAsync"/> and permissions
    /// server-side before any write.
    /// </summary>
    public async Task<IActionResult> OnPostConfirmAsync()
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        await LoadContextAsync();
        PreviewReady = true;
        ResolveDestination();

        if (!WritesEnabled)
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            _consoleLog.Log("Writes disabled — Confirm skipped.");
            return Page();
        }

        if (!Permissions.CanManageCases || !Permissions.CanManageRuns)
        {
            ErrorMessage = "Your TestRail account lacks permission to create cases, plans, or runs.";
            _consoleLog.Log($"Confirm blocked — permissions insufficient (cases={Permissions.CanManageCases}, runs={Permissions.CanManageRuns}).");
            return Page();
        }

        var validation = ValidateForCommit();
        if (validation is not null)
        {
            ErrorMessage = validation;
            return Page();
        }

        // 0. Resolve / create the destination section.
        int destinationSectionId;
        try
        {
            destinationSectionId = await ResolveOrCreateSectionAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not resolve destination section: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
            return Page();
        }

        // 0b. Duplicate-title check — skipped for brand-new sections (which can't have existing cases).
        // Compares normalized titles against cases already living in the destination section. If any
        // collide and the user hasn't explicitly ticked OverrideDuplicates, bail out with a warning
        // so they can uncheck the offending rows (or acknowledge and re-submit).
        if (!Wizard.CreateNewSection)
        {
            try
            {
                var existing = await _testRail.GetCasesBySectionAsync(Wizard.ProjectId, Wizard.SuiteId, destinationSectionId);
                var existingByTitle = existing
                    .GroupBy(c => NormalizeTitle(c.Title))
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .ToDictionary(g => g.Key, g => g.First().Id);

                var duplicateCount = 0;
                foreach (var draft in Wizard.Cases.Where(c => c.Include))
                {
                    var key = NormalizeTitle(draft.Title);
                    if (!string.IsNullOrEmpty(key) && existingByTitle.TryGetValue(key, out var existingId))
                    {
                        draft.DuplicateOfCaseId = existingId;
                        duplicateCount++;
                    }
                    else
                    {
                        draft.DuplicateOfCaseId = null;
                    }
                }

                if (duplicateCount > 0 && !Wizard.OverrideDuplicates)
                {
                    ErrorMessage =
                        $"{duplicateCount} case(s) already exist with the same title in the destination section. " +
                        "Uncheck those rows, rename them, or tick \"Create anyway\" below to proceed.";
                    _consoleLog.Log($"Confirm blocked — {duplicateCount} duplicate title(s) in section #{destinationSectionId}.");
                    return Page();
                }
            }
            catch (Exception ex)
            {
                // Duplicate-check failures shouldn't block the happy path, but the user should know.
                _consoleLog.Log($"Duplicate-check skipped: {ex.Message}");
            }
        }

        // 1. Create the cases.
        var createdCaseIds = new List<int>();
        var caseFailures = new List<string>();
        foreach (var draft in Wizard.Cases.Where(c => c.Include))
        {
            try
            {
                var created = await _testRail.AddCaseAsync(destinationSectionId, BuildCaseRequest(draft));
                if (created is null)
                {
                    caseFailures.Add($"'{draft.Title}': TestRail returned no case.");
                    continue;
                }
                createdCaseIds.Add(created.Id);
                _consoleLog.Log($"Created case C{created.Id} '{created.Title}'.");
            }
            catch (Exception ex)
            {
                caseFailures.Add($"'{draft.Title}': {ex.Message}");
                _consoleLog.Log($"Case create failed for '{draft.Title}': {ex.Message}");
            }
        }

        if (createdCaseIds.Count == 0)
        {
            ErrorMessage = "No cases were created, so no plan was created. Details: " + string.Join(" | ", caseFailures);
            return Page();
        }

        // 2. Create the plan.
        TestPlan? plan;
        try
        {
            plan = await _testRail.AddPlanAsync(Wizard.ProjectId, new TestPlanRequest
            {
                Name = Wizard.PlanName,
                Description = Wizard.PlanDescription
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Created {createdCaseIds.Count} case(s) ({string.Join(", ", createdCaseIds.Select(id => "C" + id))}) but plan creation failed: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
            return Page();
        }

        if (plan is null)
        {
            ErrorMessage = $"Created {createdCaseIds.Count} case(s) but the plan endpoint returned nothing.";
            return Page();
        }

        // 3. Create the plan entry (run).
        try
        {
            var entry = await _testRail.AddPlanEntryAsync(plan.Id, new TestPlanEntryRequest
            {
                SuiteId = Wizard.SuiteId,
                Name = Wizard.RunName,
                IncludeAll = false,
                CaseIds = createdCaseIds
            });
            if (entry is null)
            {
                ErrorMessage = $"Plan P{plan.Id} created, but the run could not be added. Add it manually from Plan Details.";
                CreatedPlanId = plan.Id;
                return Page();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Plan P{plan.Id} created but the run could not be added: {ex.Message}";
            CreatedPlanId = plan.Id;
            return Page();
        }

        _consoleLog.Log($"Confirm succeeded: plan P{plan.Id} with {createdCaseIds.Count} case(s).");
        var partial = caseFailures.Count > 0 ? $" ({caseFailures.Count} case(s) failed and were skipped.)" : string.Empty;
        TempData["SuccessMessage"] = $"Plan P{plan.Id} created from {Wizard.StoryId?.ToString() ?? "story"} with {createdCaseIds.Count} case(s).{partial}";
        return RedirectToPage("/PlanDetail", new { planId = plan.Id });
    }

    private async Task LoadContextAsync()
    {
        Permissions = await _permissionService.GetPermissionsAsync();
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
        var settings = await _settingsService.GetSettingsAsync();
        AzureDevOpsConfigured = !string.IsNullOrWhiteSpace(settings?.AzureDevOpsPat);

        try
        {
            Projects = (await _testRail.GetProjectsAsync())
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (Wizard.ProjectId > 0)
            {
                Suites = (await _testRail.GetSuitesAsync(Wizard.ProjectId))
                    .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (Wizard.ProjectId > 0 && Wizard.SuiteId > 0)
            {
                Sections = await _testRail.GetSectionsAsync(Wizard.ProjectId, Wizard.SuiteId);
                SectionTree = SectionTreeBuilder.Build(Sections);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load TestRail data: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }
    }

    private void ResolveDestination()
    {
        // Precedence: explicit SectionId > typed path > create-new.
        if (Wizard.SectionId is int sid && sid > 0)
        {
            var node = SectionTreeBuilder.FindById(SectionTree, sid);
            ResolvedDestinationBreadcrumb = SectionTreeBuilder.FormatBreadcrumb(node) ?? $"Section #{sid}";
            return;
        }

        if (!string.IsNullOrWhiteSpace(Wizard.SectionPathInput))
        {
            var result = SectionTreeBuilder.ResolvePath(SectionTree, Wizard.SectionPathInput);
            switch (result.Status)
            {
                case SectionPathStatus.Resolved:
                    Wizard.SectionId = result.SectionId;
                    ResolvedDestinationBreadcrumb = SectionTreeBuilder.FormatBreadcrumb(result.DeepestResolved);
                    break;
                case SectionPathStatus.Missing:
                case SectionPathStatus.Ambiguous:
                    DestinationMessage = result.Message;
                    break;
            }
            return;
        }

        if (Wizard.CreateNewSection
            && Wizard.NewSectionParentId is int parentId
            && parentId > 0
            && !string.IsNullOrWhiteSpace(Wizard.NewSectionName))
        {
            var parent = SectionTreeBuilder.FindById(SectionTree, parentId);
            var parentBreadcrumb = SectionTreeBuilder.FormatBreadcrumb(parent) ?? $"Section #{parentId}";
            ResolvedDestinationBreadcrumb = $"{parentBreadcrumb} › {Wizard.NewSectionName} (to be created)";
        }
    }

    private string? ValidateForCommit()
    {
        if (Wizard.ProjectId <= 0 || Wizard.SuiteId <= 0)
        {
            return "Project and suite are required.";
        }

        if (string.IsNullOrWhiteSpace(Wizard.PlanName))
        {
            return "Plan name is required.";
        }

        if (string.IsNullOrWhiteSpace(Wizard.RunName))
        {
            return "Run name is required.";
        }

        if (!Wizard.Cases.Any(c => c.Include && !string.IsNullOrWhiteSpace(c.Title)))
        {
            return "Select at least one case to include (and give it a title).";
        }

        var destinationPicked = (Wizard.SectionId is > 0)
            || (Wizard.CreateNewSection && Wizard.NewSectionParentId is > 0 && !string.IsNullOrWhiteSpace(Wizard.NewSectionName));

        if (!destinationPicked)
        {
            return "Pick a destination section (from the tree or a typed path), or opt in to creating a new child section.";
        }

        return null;
    }

    private async Task<int> ResolveOrCreateSectionAsync()
    {
        if (Wizard.CreateNewSection
            && Wizard.NewSectionParentId is int parentId
            && parentId > 0
            && !string.IsNullOrWhiteSpace(Wizard.NewSectionName))
        {
            var newSection = await _testRail.AddSectionAsync(
                Wizard.ProjectId,
                Wizard.SuiteId,
                Wizard.NewSectionName!,
                parentId,
                description: null);

            if (newSection is null)
            {
                throw new InvalidOperationException("TestRail did not return a section after creation.");
            }

            _consoleLog.Log($"Created section S{newSection.Id} '{newSection.Name}' under parent #{parentId}.");
            return newSection.Id;
        }

        if (Wizard.SectionId is int sid && sid > 0)
        {
            return sid;
        }

        throw new InvalidOperationException("No destination section was resolved.");
    }

    /// <summary>
    /// Normalizes a case title for duplicate comparison: lowercase, trimmed, internal whitespace collapsed.
    /// Two titles that differ only in casing or spacing are treated as the same case.
    /// </summary>
    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        var collapsed = System.Text.RegularExpressions.Regex.Replace(title.Trim(), @"\s+", " ");
        return collapsed.ToLowerInvariant();
    }

    private static AddTestCaseRequest BuildCaseRequest(CaseDraft draft) => new()
    {
        Title = string.IsNullOrWhiteSpace(draft.Title) ? "(untitled)" : draft.Title,
        TypeId = draft.TypeId,
        PriorityId = draft.PriorityId,
        TemplateId = draft.TemplateId,
        Estimate = string.IsNullOrWhiteSpace(draft.Estimate) ? null : draft.Estimate,
        Refs = string.IsNullOrWhiteSpace(draft.Refs) ? null : draft.Refs,
        Preconditions = string.IsNullOrWhiteSpace(draft.Preconditions) ? null : draft.Preconditions,
        Steps = string.IsNullOrWhiteSpace(draft.Steps) ? null : draft.Steps,
        ExpectedResult = string.IsNullOrWhiteSpace(draft.Expected) ? null : draft.Expected
    };
}
