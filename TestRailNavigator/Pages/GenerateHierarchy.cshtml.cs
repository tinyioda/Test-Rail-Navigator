using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestRailNavigator.Models;
using TestRailNavigator.Services;

namespace TestRailNavigator.Pages;

/// <summary>
/// Page model for generating a full TestRail hierarchy (milestones → child milestones → plans →
/// runs → cases) from a single Azure DevOps (or, eventually, Jira) URL.
/// </summary>
public class GenerateHierarchyModel : PageModel
{
    private readonly TestRailClient _testRail;
    private readonly IEnumerable<IIssueTrackerClient> _trackers;
    private readonly HierarchyGenerator _generator;
    private readonly SettingsService _settingsService;
    private readonly ConsoleLogService _consoleLog;
    private readonly PermissionService _permissionService;

    /// <summary>Initializes a new instance of the <see cref="GenerateHierarchyModel"/> class.</summary>
    public GenerateHierarchyModel(
        TestRailClient testRail,
        IEnumerable<IIssueTrackerClient> trackers,
        HierarchyGenerator generator,
        SettingsService settingsService,
        ConsoleLogService consoleLog,
        PermissionService permissionService)
    {
        _testRail = testRail;
        _trackers = trackers;
        _generator = generator;
        _settingsService = settingsService;
        _consoleLog = consoleLog;
        _permissionService = permissionService;
    }

    /// <summary>Gets or sets the current user's permissions.</summary>
    public TestRailPermissions Permissions { get; set; } = TestRailPermissions.ReadOnly();

    /// <summary>Gets or sets a value indicating whether write operations are enabled.</summary>
    public bool WritesEnabled { get; set; }

    /// <summary>Gets or sets the list of projects (alphabetical).</summary>
    public List<Project> Projects { get; set; } = [];

    /// <summary>Gets or sets the list of suites for the selected project (alphabetical).</summary>
    public List<Suite> Suites { get; set; } = [];

    /// <summary>Gets or sets the existing milestones in the selected project (alphabetical).</summary>
    public List<Milestone> Milestones { get; set; } = [];

    /// <summary>Gets or sets the selected project identifier.</summary>
    [BindProperty(SupportsGet = true)]
    public int? ProjectId { get; set; }

    /// <summary>Gets or sets the selected suite identifier.</summary>
    [BindProperty(SupportsGet = true)]
    public int? SuiteId { get; set; }

    /// <summary>Gets or sets the optional parent milestone id used when the root is not an Epic.</summary>
    [BindProperty]
    public int? ParentMilestoneId { get; set; }

    /// <summary>Gets or sets the source URL of the root work item.</summary>
    [BindProperty]
    public string? RootUrl { get; set; }

    /// <summary>Gets or sets the default type id applied to every generated case.</summary>
    [BindProperty]
    public int? DefaultTypeId { get; set; }

    /// <summary>Gets or sets the default priority id applied to every generated case.</summary>
    [BindProperty]
    public int? DefaultPriorityId { get; set; }

    /// <summary>Gets or sets the banner error message.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the banner success message.</summary>
    public string? SuccessMessage { get; set; }

    /// <summary>Gets or sets the report from the most recent generation run.</summary>
    public HierarchyReport? Report { get; set; }

    /// <summary>Gets or sets the list of configured tracker display names.</summary>
    public List<string> ConfiguredTrackers { get; set; } = [];

    /// <summary>Gets or sets the list of trackers that recognize URLs but are not configured.</summary>
    public List<string> UnconfiguredTrackers { get; set; } = [];

    /// <summary>Handles GET requests; populates dropdowns.</summary>
    public async Task<IActionResult> OnGetAsync()
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        await LoadPickersAsync();
        await LoadTrackerStatusAsync();

        Permissions = await _permissionService.GetPermissionsAsync();
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
        return Page();
    }

    /// <summary>Handles POST; walks the tracker hierarchy and builds the TestRail tree.</summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!await _settingsService.IsConfiguredAsync())
        {
            return RedirectToPage("/Setup");
        }

        Permissions = await _permissionService.GetPermissionsAsync();
        WritesEnabled = await _settingsService.AreWritesEnabledAsync();
        await LoadTrackerStatusAsync();

        if (!WritesEnabled)
        {
            ErrorMessage = "Write operations are disabled. Enable AllowWrites in settings.";
            await LoadPickersAsync();
            return Page();
        }

        if (ProjectId is not > 0 || SuiteId is not > 0)
        {
            ErrorMessage = "Select a project and suite before generating.";
            await LoadPickersAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(RootUrl))
        {
            ErrorMessage = "Paste the Azure DevOps URL of the Epic, Feature, or Story to seed the hierarchy from.";
            await LoadPickersAsync();
            return Page();
        }

        var tracker = _trackers.FirstOrDefault(t => t.CanHandle(RootUrl.Trim()));
        if (tracker is null)
        {
            ErrorMessage = "No issue tracker recognized that URL. Azure DevOps work item links are supported today; Jira is coming soon.";
            await LoadPickersAsync();
            return Page();
        }

        if (!await tracker.IsConfiguredAsync())
        {
            ErrorMessage = $"{tracker.DisplayName} is not configured. Add credentials on the Setup page first.";
            await LoadPickersAsync();
            return Page();
        }

        try
        {
            var rootItem = await tracker.GetItemAsync(RootUrl.Trim());
            if (rootItem is null)
            {
                ErrorMessage = "The URL was recognized but the work item could not be fetched.";
                await LoadPickersAsync();
                return Page();
            }

            var request = new HierarchyRequest
            {
                ProjectId = ProjectId.Value,
                SuiteId = SuiteId.Value,
                ParentMilestoneId = ParentMilestoneId,
                DefaultTypeId = DefaultTypeId,
                DefaultPriorityId = DefaultPriorityId
            };

            Report = await _generator.GenerateAsync(tracker, rootItem, request);

            var summary =
                $"Created {Report.MilestonesCreated.Count} milestone(s), " +
                $"{Report.PlansCreated.Count} plan(s), " +
                $"{Report.RunsCreated.Count} run(s), " +
                $"{Report.CasesCreated.Count} case(s).";
            var reused =
                Report.MilestonesReused.Count + Report.PlansReused.Count + Report.SectionsReused.Count;
            if (reused > 0)
            {
                summary += $" Reused {reused} existing entit{(reused == 1 ? "y" : "ies")}.";
            }

            SuccessMessage = summary;
            _consoleLog.Log(summary);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Generation failed: {ex.Message}";
            _consoleLog.Log(ErrorMessage);
        }

        await LoadPickersAsync();
        return Page();
    }

    private async Task LoadPickersAsync()
    {
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

                Milestones = (await _testRail.GetMilestonesAsync(ProjectId.Value))
                    .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage ??= $"Failed to load project data: {ex.Message}";
            _consoleLog.Log($"GenerateHierarchy: {ex.Message}");
        }
    }

    private async Task LoadTrackerStatusAsync()
    {
        ConfiguredTrackers.Clear();
        UnconfiguredTrackers.Clear();
        foreach (var tracker in _trackers)
        {
            if (await tracker.IsConfiguredAsync())
            {
                ConfiguredTrackers.Add(tracker.DisplayName);
            }
            else
            {
                UnconfiguredTrackers.Add(tracker.DisplayName);
            }
        }
    }
}
