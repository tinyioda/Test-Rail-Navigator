namespace TestRailNavigator.Models;

/// <summary>
/// Classification of an issue tracker work item, used to decide how it maps into TestRail.
/// Unknown types are treated as leaves (plans/cases) rather than container milestones.
/// </summary>
public enum IssueTrackerItemKind
{
    /// <summary>The kind could not be determined from the source system.</summary>
    Unknown = 0,

    /// <summary>A top-level container (Epic). Mapped to a root TestRail milestone.</summary>
    Epic,

    /// <summary>A mid-level container (Feature). Mapped to a child milestone.</summary>
    Feature,

    /// <summary>A deliverable work item (User Story, PBI, Requirement). Mapped to a test plan + run.</summary>
    Story,

    /// <summary>A defect. Mapped to a test plan + run (or individual case if used as a leaf).</summary>
    Bug,

    /// <summary>An implementation sub-item (Task). Usually skipped when it has no AC.</summary>
    Task,

    /// <summary>An existing manual test case definition in the source tracker.</summary>
    TestCase
}

/// <summary>
/// Tracker-agnostic representation of a work item fetched from Azure DevOps or Jira.
/// </summary>
public class IssueTrackerItem
{
    /// <summary>Gets or sets the tracker-native identifier (e.g. ADO work item id, Jira issue key).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the numeric id when the tracker exposes one (used for TestRail <c>refs</c>).</summary>
    public int? NumericId { get; set; }

    /// <summary>Gets or sets the reference string written into TestRail case <c>refs</c> (e.g. "AB#123", "PROJ-42").</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the classified work item kind.</summary>
    public IssueTrackerItemKind Kind { get; set; }

    /// <summary>Gets or sets the raw tracker-specific type string (e.g. "User Story", "Product Backlog Item").</summary>
    public string RawType { get; set; } = string.Empty;

    /// <summary>Gets or sets the item title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the HTML description, if any.</summary>
    public string? DescriptionHtml { get; set; }

    /// <summary>Gets or sets the HTML acceptance criteria, if any.</summary>
    public string? AcceptanceCriteriaHtml { get; set; }

    /// <summary>Gets or sets the HTML repro steps, if any (typically for Bug work items).</summary>
    public string? ReproStepsHtml { get; set; }

    /// <summary>Gets or sets the canonical URL to the item in the source tracker.</summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the URLs of direct hierarchy children (used by <c>GetChildrenAsync</c>).</summary>
    public List<string> ChildUrls { get; set; } = [];
}
