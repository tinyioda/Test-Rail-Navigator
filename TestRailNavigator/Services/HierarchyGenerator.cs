using System.Text.RegularExpressions;
using TestRailNavigator.Models;

namespace TestRailNavigator.Services;

/// <summary>
/// Orchestrates building a TestRail hierarchy (milestones → child milestones → plans → runs → cases)
/// from an issue tracker work item tree.
/// </summary>
/// <remarks>
/// Mapping:
/// <list type="bullet">
///   <item>Epic → root Milestone</item>
///   <item>Feature → child Milestone (parent = Epic milestone)</item>
///   <item>User Story / PBI / Requirement / Bug → Test Plan (associated with the nearest milestone)</item>
///   <item>Acceptance Criterion → Test Case (inside a Section named after the Story)</item>
///   <item>Each plan receives a single Plan Entry + Run containing the generated cases.</item>
/// </list>
/// Existing entities are reused when their name starts with the canonical <c>[ref] </c> prefix
/// (e.g. <c>[AB#123] </c>) to make re-runs idempotent.
/// </remarks>
public class HierarchyGenerator
{
    private static readonly Regex ReferencePrefixRegex = new(@"^\s*\[(?<ref>[^\]]+)\]", RegexOptions.Compiled);

    private readonly TestRailClient _testRail;
    private readonly ConsoleLogService _consoleLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchyGenerator"/> class.
    /// </summary>
    public HierarchyGenerator(TestRailClient testRail, ConsoleLogService consoleLog)
    {
        _testRail = testRail;
        _consoleLog = consoleLog;
    }

    /// <summary>
    /// Generates the TestRail hierarchy beneath the supplied root work item.
    /// </summary>
    /// <param name="tracker">The issue tracker client that produced the root item.</param>
    /// <param name="rootItem">The root work item (Epic, Feature, or Story).</param>
    /// <param name="request">Destination parameters (project/suite/etc.).</param>
    /// <returns>A report describing everything that was created or reused.</returns>
    public async Task<HierarchyReport> GenerateAsync(
        IIssueTrackerClient tracker,
        IssueTrackerItem rootItem,
        HierarchyRequest request)
    {
        var report = new HierarchyReport { Root = rootItem };

        // Pre-fetch existing milestones/plans/sections once so the walker can match by prefix.
        var existingMilestones = await _testRail.GetMilestonesAsync(request.ProjectId);
        var existingPlans = await _testRail.GetPlansAsync(request.ProjectId);
        var existingSections = await _testRail.GetSectionsAsync(request.ProjectId, request.SuiteId);
        var milestoneIndex = BuildReferenceIndex(existingMilestones, m => m.Id, m => m.Name);
        var planIndex = BuildReferenceIndex(existingPlans, p => p.Id, p => p.Name);
        var sectionIndex = BuildReferenceIndex(existingSections, s => s.Id, s => s.Name);

        // Branch on the root kind to decide how deep to walk.
        switch (rootItem.Kind)
        {
            case IssueTrackerItemKind.Epic:
                {
                    var epicMilestoneId = await EnsureMilestoneAsync(request.ProjectId, rootItem, null, milestoneIndex, report);
                    var features = await tracker.GetChildrenAsync(rootItem);
                    foreach (var feature in features.OrderBy(f => f.Title, StringComparer.OrdinalIgnoreCase))
                    {
                        if (feature.Kind == IssueTrackerItemKind.Feature)
                        {
                            var featureMilestoneId = await EnsureMilestoneAsync(request.ProjectId, feature, epicMilestoneId, milestoneIndex, report);
                            var stories = await tracker.GetChildrenAsync(feature);
                            foreach (var story in stories.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase))
                            {
                                await ProcessLeafAsync(story, featureMilestoneId, request, planIndex, sectionIndex, report);
                            }
                        }
                        else
                        {
                            // Epic with direct Story children (no Features).
                            await ProcessLeafAsync(feature, epicMilestoneId, request, planIndex, sectionIndex, report);
                        }
                    }

                    break;
                }

            case IssueTrackerItemKind.Feature:
                {
                    var featureMilestoneId = await EnsureMilestoneAsync(request.ProjectId, rootItem, request.ParentMilestoneId, milestoneIndex, report);
                    var stories = await tracker.GetChildrenAsync(rootItem);
                    foreach (var story in stories.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase))
                    {
                        await ProcessLeafAsync(story, featureMilestoneId, request, planIndex, sectionIndex, report);
                    }

                    break;
                }

            default:
                {
                    // Story/Bug/Task/Unknown — treat as a leaf: plan + run + cases under the chosen parent milestone (if any).
                    await ProcessLeafAsync(rootItem, request.ParentMilestoneId, request, planIndex, sectionIndex, report);
                    break;
                }
        }

        return report;
    }

    private async Task ProcessLeafAsync(
        IssueTrackerItem item,
        int? milestoneId,
        HierarchyRequest request,
        Dictionary<string, int> planIndex,
        Dictionary<string, int> sectionIndex,
        HierarchyReport report)
    {
        // 1. Create the cases from acceptance criteria (or repro steps for Bugs).
        var sourceHtml = item.AcceptanceCriteriaHtml;
        if (string.IsNullOrWhiteSpace(sourceHtml) && !string.IsNullOrWhiteSpace(item.ReproStepsHtml))
        {
            sourceHtml = item.ReproStepsHtml;
        }

        var candidates = AcceptanceCriteriaParser.Parse(sourceHtml, item.Title);

        var sectionId = await EnsureSectionAsync(request.ProjectId, request.SuiteId, item, sectionIndex, report);
        var caseIds = new List<int>();
        foreach (var candidate in candidates)
        {
            var addRequest = new AddTestCaseRequest
            {
                Title = candidate.Title,
                Refs = item.Reference,
                PriorityId = request.DefaultPriorityId,
                TypeId = request.DefaultTypeId,
                Steps = candidate.Steps,
                ExpectedResult = candidate.Expected,
                Preconditions = AzureDevOpsService.HtmlToPlainText(item.DescriptionHtml)
            };

            try
            {
                var created = await _testRail.AddCaseAsync(sectionId, addRequest);
                if (created is not null)
                {
                    caseIds.Add(created.Id);
                    report.CasesCreated.Add(new EntityRef(created.Id, created.Title ?? candidate.Title, item.Reference));
                }
            }
            catch (Exception ex)
            {
                report.Warnings.Add($"Failed to create case '{candidate.Title}' under {item.Reference}: {ex.Message}");
                _consoleLog.Log(report.Warnings[^1]);
            }
        }

        // 2. Ensure the plan exists, attached to the nearest milestone.
        var planName = CanonicalName(item);
        int planId;
        if (planIndex.TryGetValue(item.Reference, out var existingPlanId))
        {
            planId = existingPlanId;
            report.PlansReused.Add(new EntityRef(planId, planName, item.Reference));
        }
        else
        {
            var plan = await _testRail.AddPlanAsync(request.ProjectId, new TestPlanRequest
            {
                Name = planName,
                Description = BuildDescription(item),
                MilestoneId = milestoneId
            });

            if (plan is null)
            {
                report.Warnings.Add($"TestRail did not return a plan when creating '{planName}'.");
                return;
            }

            planId = plan.Id;
            planIndex[item.Reference] = planId;
            report.PlansCreated.Add(new EntityRef(planId, planName, item.Reference));
        }

        // 3. Add a plan entry (= run) containing just the newly-created cases.
        if (caseIds.Count > 0)
        {
            try
            {
                var entry = await _testRail.AddPlanEntryAsync(planId, new TestPlanEntryRequest
                {
                    SuiteId = request.SuiteId,
                    Name = TruncateName(item.Title, 250),
                    IncludeAll = false,
                    CaseIds = caseIds
                });

                if (entry is not null)
                {
                    foreach (var run in entry.Runs)
                    {
                        report.RunsCreated.Add(new EntityRef(run.Id, run.Name, item.Reference));
                    }
                }
            }
            catch (Exception ex)
            {
                report.Warnings.Add($"Failed to add plan entry for {item.Reference}: {ex.Message}");
                _consoleLog.Log(report.Warnings[^1]);
            }
        }
        else
        {
            report.Warnings.Add($"No test cases produced for {item.Reference} — run was not created.");
        }
    }

    private async Task<int> EnsureMilestoneAsync(
        int projectId,
        IssueTrackerItem item,
        int? parentMilestoneId,
        Dictionary<string, int> index,
        HierarchyReport report)
    {
        var name = CanonicalName(item);
        if (index.TryGetValue(item.Reference, out var existingId))
        {
            report.MilestonesReused.Add(new EntityRef(existingId, name, item.Reference));
            return existingId;
        }

        var milestone = await _testRail.AddMilestoneAsync(projectId, new MilestoneRequest
        {
            Name = name,
            Description = BuildDescription(item),
            ParentId = parentMilestoneId
        });

        if (milestone is null)
        {
            throw new InvalidOperationException($"TestRail did not return a milestone when creating '{name}'.");
        }

        index[item.Reference] = milestone.Id;
        report.MilestonesCreated.Add(new EntityRef(milestone.Id, name, item.Reference));
        return milestone.Id;
    }

    private async Task<int> EnsureSectionAsync(
        int projectId,
        int suiteId,
        IssueTrackerItem item,
        Dictionary<string, int> index,
        HierarchyReport report)
    {
        var name = CanonicalName(item);
        if (index.TryGetValue(item.Reference, out var existingId))
        {
            report.SectionsReused.Add(new EntityRef(existingId, name, item.Reference));
            return existingId;
        }

        var section = await _testRail.AddSectionAsync(projectId, suiteId, name);
        if (section is null)
        {
            throw new InvalidOperationException($"TestRail did not return a section when creating '{name}'.");
        }

        index[item.Reference] = section.Id;
        report.SectionsCreated.Add(new EntityRef(section.Id, name, item.Reference));
        return section.Id;
    }

    private static Dictionary<string, int> BuildReferenceIndex<T>(IEnumerable<T> items, Func<T, int> idSelector, Func<T, string> nameSelector)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in items)
        {
            var match = ReferencePrefixRegex.Match(nameSelector(entity));
            if (match.Success)
            {
                var key = match.Groups["ref"].Value.Trim();
                dict.TryAdd(key, idSelector(entity));
            }
        }

        return dict;
    }

    private static string CanonicalName(IssueTrackerItem item) =>
        TruncateName($"[{item.Reference}] {item.Title}", 250);

    private static string? BuildDescription(IssueTrackerItem item)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.SourceUrl)) parts.Add($"Source: {item.SourceUrl}");
        if (!string.IsNullOrWhiteSpace(item.RawType)) parts.Add($"Type: {item.RawType}");
        var desc = AzureDevOpsService.HtmlToPlainText(item.DescriptionHtml);
        if (!string.IsNullOrWhiteSpace(desc)) parts.Add(desc!);
        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }

    private static string TruncateName(string value, int max)
    {
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }
}

/// <summary>Input parameters for <see cref="HierarchyGenerator.GenerateAsync"/>.</summary>
public class HierarchyRequest
{
    /// <summary>Gets or sets the TestRail project id where entities are created.</summary>
    public int ProjectId { get; set; }

    /// <summary>Gets or sets the TestRail suite id used for sections and plan entries.</summary>
    public int SuiteId { get; set; }

    /// <summary>
    /// Gets or sets the optional parent milestone id, used when the root item is a
    /// Feature/Story/Bug (i.e. not an Epic) and the user wants it nested under an existing milestone.
    /// </summary>
    public int? ParentMilestoneId { get; set; }

    /// <summary>Gets or sets the default type id applied to every generated case.</summary>
    public int? DefaultTypeId { get; set; }

    /// <summary>Gets or sets the default priority id applied to every generated case.</summary>
    public int? DefaultPriorityId { get; set; }
}

/// <summary>Describes a TestRail entity referenced in a <see cref="HierarchyReport"/>.</summary>
public record EntityRef(int Id, string Name, string Reference);

/// <summary>Outcome of a hierarchy generation run.</summary>
public class HierarchyReport
{
    /// <summary>Gets the root issue tracker item the walk started from.</summary>
    public IssueTrackerItem Root { get; init; } = new();

    /// <summary>Gets the milestones created during the walk.</summary>
    public List<EntityRef> MilestonesCreated { get; } = [];

    /// <summary>Gets the milestones that were reused (matched by reference prefix).</summary>
    public List<EntityRef> MilestonesReused { get; } = [];

    /// <summary>Gets the plans created during the walk.</summary>
    public List<EntityRef> PlansCreated { get; } = [];

    /// <summary>Gets the plans that were reused.</summary>
    public List<EntityRef> PlansReused { get; } = [];

    /// <summary>Gets the runs that were created via plan entries.</summary>
    public List<EntityRef> RunsCreated { get; } = [];

    /// <summary>Gets the sections created during the walk.</summary>
    public List<EntityRef> SectionsCreated { get; } = [];

    /// <summary>Gets the sections that were reused.</summary>
    public List<EntityRef> SectionsReused { get; } = [];

    /// <summary>Gets the cases created during the walk.</summary>
    public List<EntityRef> CasesCreated { get; } = [];

    /// <summary>Gets the non-fatal warnings accumulated during the walk.</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>Gets a value indicating whether the run produced any content.</summary>
    public bool HasAnyWork =>
        MilestonesCreated.Count + MilestonesReused.Count +
        PlansCreated.Count + PlansReused.Count +
        RunsCreated.Count + CasesCreated.Count > 0;
}
