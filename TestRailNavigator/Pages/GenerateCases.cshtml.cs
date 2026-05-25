using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Models;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>
/// Page model for generating TestRail test cases from a single Azure DevOps work item URL.
/// </summary>
/// <remarks>
/// The flow is two-step. On <c>OnPostLoadAsync</c> the page fetches the AzDO item, parses its
/// acceptance criteria into one <see cref="CaseDraft"/> per criterion, and resolves the destination
/// section path. No TestRail write happens until the user clicks the Confirm-modal's "Yes, create"
/// button on <c>OnPostConfirmAsync</c>. The destination can be an existing section, a fully-existing
/// typed path, or a typed path with any number of new sub-sections to be created (with the user's
/// explicit opt-in).
/// </remarks>
public class GenerateCasesModel : PageModel
{
    private readonly TestRailClient _testRail;
    private readonly AzureDevOpsService _azureDevOps;
    private readonly SettingsService _settingsService;
    private readonly ConsoleLogService _consoleLog;
    private readonly PermissionService _permissionService;
    private readonly ICaseEnrichmentService _enrichment;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateCasesModel"/> class.
    /// </summary>
    public GenerateCasesModel(
        TestRailClient testRail,
        AzureDevOpsService azureDevOps,
        SettingsService settingsService,
        ConsoleLogService consoleLog,
        PermissionService permissionService,
        ICaseEnrichmentService enrichment)
    {
        _testRail = testRail;
        _azureDevOps = azureDevOps;
        _settingsService = settingsService;
        _consoleLog = consoleLog;
        _permissionService = permissionService;
        _enrichment = enrichment;
    }

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

    /// <summary>Gets or sets the rooted section tree for the typed-path resolver.</summary>
    public IReadOnlyList<SectionTreeNode> SectionTree { get; set; } = [];

    /// <summary>Gets or sets the selected project identifier.</summary>
    [BindProperty(SupportsGet = true)]
    public int? ProjectId { get; set; }

    /// <summary>Gets or sets the selected suite identifier.</summary>
    [BindProperty(SupportsGet = true)]
    public int? SuiteId { get; set; }

    /// <summary>
    /// Gets or sets the destination section identifier when an existing section is picked
    /// directly from the combobox. If <see cref="NewSectionName"/> is also set, the new
    /// sub-section is created as a child of this section.
    /// </summary>
    [BindProperty]
    public int? SectionId { get; set; }

    /// <summary>
    /// Gets or sets an optional new sub-section name. When non-empty, a new section with this
    /// name is created under <see cref="SectionId"/> (or at the suite root if no section is
    /// picked) and cases are placed inside it. Making this its own field — instead of a typed
    /// slash path — keeps the "I'm creating something new" intent explicit.
    /// </summary>
    [BindProperty]
    public string? NewSectionName { get; set; }

    /// <summary>Gets or sets the optional type identifier applied to every generated case.</summary>
    [BindProperty]
    public int? CaseTypeId { get; set; }

    /// <summary>Gets or sets the optional priority identifier applied to every generated case.</summary>
    [BindProperty]
    public int? CasePriorityId { get; set; }

    /// <summary>Gets or sets the Azure DevOps work item URL to import.</summary>
    [BindProperty]
    public string? WorkItemUrl { get; set; }

    /// <summary>Gets or sets the numeric AzDO work item id (display only; re-validated from <see cref="WorkItemUrl"/> on Confirm).</summary>
    [BindProperty]
    public int? StoryId { get; set; }

    /// <summary>Gets or sets the AzDO work item title (display only).</summary>
    [BindProperty]
    public string? StoryTitle { get; set; }

    /// <summary>Gets or sets the AzDO work item type (display only).</summary>
    [BindProperty]
    public string? StoryType { get; set; }

    /// <summary>Gets or sets the detected AC format string reported by the parser (display only).</summary>
    [BindProperty]
    public string? DetectedAcFormat { get; set; }

    /// <summary>Gets or sets the detected AC item count reported by the parser (display only).</summary>
    [BindProperty]
    public int? DetectedAcItemCount { get; set; }

    /// <summary>Gets or sets the list of case drafts derived from the story's ACs.</summary>
    [BindProperty]
    public List<CaseDraft> Cases { get; set; } = [];

    /// <summary>Gets or sets the resolved destination breadcrumb shown on the review step.</summary>
    public string? ResolvedDestinationBreadcrumb { get; set; }

    /// <summary>Gets or sets the path-planner diagnostic message (missing/ambiguous segments) for display.</summary>
    public string? DestinationMessage { get; set; }

    /// <summary>Gets or sets the list of path segments that will be created (empty when the path fully exists).</summary>
    public IReadOnlyList<string> PlannedNewSegments { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether the Load step produced a valid preview.</summary>
    public bool PreviewReady { get; set; }

    /// <summary>Gets or sets the per-case generation results shown after a successful Confirm.</summary>
    public List<GenerationResult> Results { get; set; } = [];

    /// <summary>Gets or sets the error banner message.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the success banner message.</summary>
    public string? SuccessMessage { get; set; }

    /// <summary>Gets or sets a value indicating whether an LLM endpoint is configured (drives the Enrich button).</summary>
    public bool EnrichmentConfigured { get; set; }

    /// <summary>Gets or sets a short display name for the configured LLM endpoint.</summary>
    public string EnrichmentDisplayName { get; set; } = "(not configured)";

    /// <summary>
    /// Hidden form field carrying the plain-text rendering of the work item's Acceptance Criteria
    /// across the Load -> Enrich/Confirm round trip. Used as LLM context so the model can see the
    /// full AC block when authoring a single criterion.
    /// </summary>
    [BindProperty]
    public string? AllAcPlainText { get; set; }

    /// <summary>
    /// Hidden form field carrying the plain-text Description across round trips, for LLM context.
    /// </summary>
    [BindProperty]
    public string? StoryDescriptionPlainText { get; set; }

    /// <summary>
    /// GET handler — loads pickers and displays an empty form.
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
    /// POST handler for the Load button — fetches the AzDO story, parses AC into case drafts,
    /// and plans the destination section path. Does not write to TestRail.
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

        if (string.IsNullOrWhiteSpace(WorkItemUrl))
        {
            ErrorMessage = "Enter an Azure DevOps work item URL.";
            return Page();
        }

        // Project is not required to *load* the work item — it's only needed when the user
        // chooses a destination and clicks Confirm. Letting Load run without a project means
        // the user can paste a URL, review the AC-derived cases, and pick the project after.

        IssueTrackerItem? item;
        try
        {
            item = await _azureDevOps.GetItemAsync(WorkItemUrl);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to fetch work item: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
            return Page();
        }

        if (item is null)
        {
            ErrorMessage = "The URL did not resolve to a recognizable Azure DevOps work item.";
            return Page();
        }

        StoryId = item.NumericId;
        StoryTitle = item.Title;
        StoryType = item.RawType;

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
        DetectedAcFormat = parseResult.Diagnostics.DetectedFormat;
        DetectedAcItemCount = parseResult.Diagnostics.ItemCount;

        var precondsDefault = AzureDevOpsService.HtmlToPlainText(item.DescriptionHtml);
        StoryDescriptionPlainText = precondsDefault;
        AllAcPlainText = AzureDevOpsService.HtmlToPlainText(sourceHtml);
        Cases = parseResult.Candidates.Select(c => new CaseDraft
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

        ResolveDestination();
        PreviewReady = true;
        _consoleLog.Log($"Loaded work item {item.Reference}: {parseResult.Diagnostics.ItemCount} case(s) from '{parseResult.Diagnostics.DetectedFormat}'.");
        return Page();
    }

    /// <summary>
    /// POST handler for the Confirm button — ensures the destination path exists (creating missing
    /// sub-sections if opted in), then creates one test case per included <see cref="CaseDraft"/>.
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
            _consoleLog.Log("Writes disabled — GenerateCases Confirm skipped.");
            return Page();
        }

        if (!Permissions.CanManageCases)
        {
            ErrorMessage = "Your TestRail account lacks permission to create cases.";
            _consoleLog.Log($"Confirm blocked — CanManageCases={Permissions.CanManageCases}.");
            return Page();
        }

        if (ProjectId is not > 0 || SuiteId is not > 0)
        {
            ErrorMessage = "Project is required.";
            return Page();
        }

        var includedCases = Cases.Where(c => c.Include && !string.IsNullOrWhiteSpace(c.Title)).ToList();
        if (includedCases.Count == 0)
        {
            ErrorMessage = "Select at least one case to include (and give it a title).";
            return Page();
        }

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

        foreach (var draft in includedCases)
        {
            Results.Add(await GenerateSingleCaseAsync(destinationSectionId, draft));
        }

        var created = Results.Count(r => r.Success);
        if (created > 0)
        {
            SuccessMessage = created == 1
                ? "Generated 1 test case from Azure DevOps."
                : $"Generated {created} test cases from Azure DevOps.";
        }

        var failed = Results.Count - created;
        if (failed > 0)
        {
            ErrorMessage = failed == 1
                ? "1 case could not be created. See details below."
                : $"{failed} cases could not be created. See details below.";
        }

        return Page();
    }

    /// <summary>
    /// POST handler for the "Enrich with AI" button. Calls the configured LLM endpoint to
    /// transform each included parser-scaffold draft into a fully-authored TestRail case
    /// (structured preconditions, summary, separated step/expected pairs). On any per-card
    /// failure the original draft is preserved and an error appended to <see cref="ErrorMessage"/>.
    /// Does not write to TestRail.
    /// </summary>
    public async Task<IActionResult> OnPostEnrichAsync()
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        await LoadContextAsync();
        PreviewReady = true;
        ResolveDestination();

        if (!await _enrichment.IsConfiguredAsync())
        {
            ErrorMessage = "AI enrichment isn't configured. Set OpenAiEndpoint, OpenAiApiKey and OpenAiModel in testrail-settings.json (Setup page).";
            return Page();
        }

        if (Cases.Count == 0)
        {
            ErrorMessage = "Load a story first; there are no cases to enrich.";
            return Page();
        }

        var ctx = new StoryEnrichmentContext(
            StoryId,
            StoryTitle ?? string.Empty,
            StoryType ?? "Work Item",
            StoryDescriptionPlainText ?? string.Empty,
            AllAcPlainText ?? string.Empty);

        var enriched = new List<CaseDraft>(Cases.Count);
        var errors = new List<string>();
        var successCount = 0;

        for (var i = 0; i < Cases.Count; i++)
        {
            var draft = Cases[i];
            if (!draft.Include || draft.Enriched)
            {
                enriched.Add(draft);
                continue;
            }

            try
            {
                var result = await _enrichment.EnrichAsync(draft, ctx, HttpContext.RequestAborted);
                enriched.Add(result);
                successCount++;
                _consoleLog.Log($"Enriched case {i + 1}/{Cases.Count}: '{result.Title}'.");
            }
            catch (Exception ex)
            {
                _consoleLog.Log($"Enrich failed for case {i + 1}: {ex.Message}");
                errors.Add($"Case {i + 1} ('{draft.Title}'): {ex.Message}");
                enriched.Add(draft);
            }
        }

        Cases = enriched;
        if (successCount > 0)
        {
            SuccessMessage = successCount == 1
                ? "Enriched 1 case with AI."
                : $"Enriched {successCount} cases with AI.";
        }
        if (errors.Count > 0)
        {
            ErrorMessage = "Some cases couldn't be enriched: " + string.Join(" | ", errors);
        }

        return Page();
    }

    private async Task<GenerationResult> GenerateSingleCaseAsync(int sectionId, CaseDraft draft)
    {
        try
        {
            var created = await _testRail.AddCaseAsync(sectionId, BuildCaseRequest(draft));
            if (created is null)
            {
                return GenerationResult.Fail(draft.Title, "TestRail did not return a case after creation.");
            }

            var message = $"Created case C{created.Id} '{created.Title}' in section #{sectionId}.";
            _consoleLog.Log(message);
            return GenerationResult.Ok(draft.Title, created.Id, created.Title ?? string.Empty);
        }
        catch (Exception ex)
        {
            _consoleLog.Log($"Failed to create case '{draft.Title}': {ex.Message}");
            return GenerationResult.Fail(draft.Title, ex.Message);
        }
    }

    private AddTestCaseRequest BuildCaseRequest(CaseDraft draft)
    {
        var hasSeparated = draft.StepsSeparated is { Count: > 0 };
        var templateId = draft.TemplateId ?? (hasSeparated ? 2 : (int?)null);
        return new AddTestCaseRequest
        {
            Title = string.IsNullOrWhiteSpace(draft.Title) ? "(untitled)" : draft.Title,
            TypeId = draft.TypeId ?? CaseTypeId,
            PriorityId = draft.PriorityId ?? CasePriorityId,
            TemplateId = templateId,
            Estimate = string.IsNullOrWhiteSpace(draft.Estimate) ? null : draft.Estimate,
            Refs = string.IsNullOrWhiteSpace(draft.Refs) ? null : draft.Refs,
            Preconditions = string.IsNullOrWhiteSpace(draft.Preconditions) ? null : draft.Preconditions,
            Summary = string.IsNullOrWhiteSpace(draft.Summary) ? null : draft.Summary,
            // When using the Steps template, send the structured steps and leave the plain
            // custom_steps/custom_expected fields null so TestRail renders the grid.
            Steps = hasSeparated ? null : (string.IsNullOrWhiteSpace(draft.Steps) ? null : draft.Steps),
            ExpectedResult = hasSeparated ? null : (string.IsNullOrWhiteSpace(draft.Expected) ? null : draft.Expected),
            StepsSeparated = hasSeparated
                ? draft.StepsSeparated!.Select(s => new Models.TestCaseStep
                {
                    Content = s.Content ?? string.Empty,
                    Expected = s.Expected ?? string.Empty
                }).ToList()
                : null
        };
    }

    private void ResolveDestination()
    {
        PlannedNewSegments = [];
        ResolvedDestinationBreadcrumb = null;
        DestinationMessage = null;

        var newName = NewSectionName?.Trim();
        var hasNewName = !string.IsNullOrWhiteSpace(newName);
        var hasSection = SectionId is int sid && sid > 0;

        if (hasNewName)
        {
            PlannedNewSegments = new[] { newName! };
            if (hasSection)
            {
                var parent = SectionTreeBuilder.FindById(SectionTree, SectionId!.Value);
                var parentCrumb = SectionTreeBuilder.FormatBreadcrumb(parent) ?? $"Section #{SectionId}";
                ResolvedDestinationBreadcrumb = $"{parentCrumb} › {newName} (to be created)";
                DestinationMessage = $"A new sub-section '{newName}' will be created under '{parentCrumb}' on Confirm.";
            }
            else
            {
                ResolvedDestinationBreadcrumb = $"{newName} (to be created at suite root)";
                DestinationMessage = $"A new top-level section '{newName}' will be created on Confirm.";
            }
            return;
        }

        if (hasSection)
        {
            var node = SectionTreeBuilder.FindById(SectionTree, SectionId!.Value);
            ResolvedDestinationBreadcrumb = SectionTreeBuilder.FormatBreadcrumb(node) ?? $"Section #{SectionId}";
        }
    }

    private async Task<int> ResolveOrCreateSectionAsync()
    {
        var newName = NewSectionName?.Trim();
        var hasNewName = !string.IsNullOrWhiteSpace(newName);

        if (!hasNewName)
        {
            if (SectionId is int sid && sid > 0)
            {
                return sid;
            }
            throw new InvalidOperationException(
                "Pick an existing section, or enter a new sub-section name to create.");
        }

        if (ProjectId is not > 0 || SuiteId is not > 0)
        {
            throw new InvalidOperationException("Project is required to create a section.");
        }

        var parentId = SectionId is int psid && psid > 0 ? (int?)psid : null;
        var created = await _testRail.AddSectionAsync(ProjectId.Value, SuiteId.Value, newName!, parentId)
            ?? throw new InvalidOperationException($"TestRail did not return a section after creating '{newName}'.");

        _consoleLog.Log(parentId is null
            ? $"Created top-level section '{newName}' (#{created.Id})."
            : $"Created sub-section '{newName}' (#{created.Id}) under section #{parentId}.");
        return created.Id;
    }

    private async Task LoadContextAsync()
    {
        Permissions = await _permissionService.GetPermissionsAsync();
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
        var settings = await _settingsService.GetSettingsAsync();
        AzureDevOpsConfigured = !string.IsNullOrWhiteSpace(settings?.AzureDevOpsPat);
        EnrichmentConfigured = await _enrichment.IsConfiguredAsync();
        EnrichmentDisplayName = await _enrichment.GetDisplayNameAsync();

        try
        {
            Projects = (await _testRail.GetProjectsAsync())
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ProjectId is > 0)
            {
                Suites = (await _testRail.GetSuitesAsync(ProjectId.Value))
                    .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Suites are an implementation detail of TestRail's API — users don't care.
                // Auto-pick the project's default suite (single-suite projects only have one,
                // which TestRail calls "Master"; multi-suite projects get the first alphabetical
                // suite, which the user can override via the SuiteId query string if needed).
                if (SuiteId is not > 0 && Suites.Count > 0)
                {
                    var master = Suites.FirstOrDefault(s =>
                        string.Equals(s.Name, "Master", StringComparison.OrdinalIgnoreCase));
                    SuiteId = master?.Id ?? Suites[0].Id;
                }
            }

            if (ProjectId is > 0 && SuiteId is > 0)
            {
                Sections = await _testRail.GetSectionsAsync(ProjectId.Value, SuiteId.Value);
                SectionTree = SectionTreeBuilder.Build(Sections);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load TestRail data: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }
    }

    /// <summary>
    /// Represents the outcome of converting a single AC draft into a TestRail case.
    /// </summary>
    public class GenerationResult
    {
        /// <summary>Gets the draft title used as the input label.</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>Gets a value indicating whether generation succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>Gets the created TestRail case identifier, when successful.</summary>
        public int? CaseId { get; init; }

        /// <summary>Gets the created TestRail case title, when successful.</summary>
        public string? CaseTitle { get; init; }

        /// <summary>Gets the error message, when generation failed.</summary>
        public string? ErrorMessage { get; init; }

        internal static GenerationResult Ok(string label, int caseId, string caseTitle) => new()
        {
            Label = label,
            Success = true,
            CaseId = caseId,
            CaseTitle = caseTitle
        };

        internal static GenerationResult Fail(string label, string error) => new()
        {
            Label = label,
            Success = false,
            ErrorMessage = error
        };
    }
}
