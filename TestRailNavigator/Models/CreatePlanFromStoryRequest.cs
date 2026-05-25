namespace TestRailNavigator.Models;

/// <summary>
/// Complete draft payload for the "Create Plan from Story" wizard.
/// </summary>
/// <remarks>
/// The page model binds a single instance of this class. It is round-tripped between the
/// Fetch, Review, Full-Edit, and Confirm handlers without any server-side persistence —
/// every relevant decision the user has made is carried in the form itself so the
/// "no TestRail write until Confirm" rule is enforced by construction.
/// <para>
/// Milestone selection is intentionally omitted from v1; <see cref="TestPlanRequest.MilestoneId"/>
/// is not populated from this payload. This will be revisited in a follow-up iteration.
/// </para>
/// </remarks>
public class CreatePlanFromStoryRequest
{
    /// <summary>
    /// Gets or sets the Azure DevOps work item URL. Authoritative source of the story on commit.
    /// </summary>
    public string StoryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the numeric AzDO work item identifier (display only; re-validated from <see cref="StoryUrl"/> on commit).
    /// </summary>
    public int? StoryId { get; set; }

    /// <summary>
    /// Gets or sets the AzDO work item title (display only).
    /// </summary>
    public string? StoryTitle { get; set; }

    /// <summary>
    /// Gets or sets the AzDO work item type (display only, e.g. "User Story", "Bug").
    /// </summary>
    public string? StoryType { get; set; }

    /// <summary>
    /// Gets or sets the destination TestRail project identifier.
    /// </summary>
    public int ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the destination TestRail suite identifier.
    /// </summary>
    public int SuiteId { get; set; }

    /// <summary>
    /// Gets or sets the destination section identifier when an existing section is picked.
    /// Mutually exclusive with <see cref="CreateNewSection"/>.
    /// </summary>
    public int? SectionId { get; set; }

    /// <summary>
    /// Gets or sets an alternative slash-delimited path to the destination section
    /// (e.g. <c>Voyager/Regression/Plan Details</c>). Resolved against the live section tree
    /// on both Load and Confirm. Ignored when <see cref="SectionId"/> is set.
    /// </summary>
    public string? SectionPathInput { get; set; }

    /// <summary>
    /// Gets or sets whether a new child section should be created on commit.
    /// When true, <see cref="NewSectionParentId"/> and <see cref="NewSectionName"/> are required.
    /// </summary>
    public bool CreateNewSection { get; set; }

    /// <summary>
    /// Gets or sets the existing parent section identifier under which the new child will be created.
    /// The parent must already exist; the feature never invents top-level sections.
    /// </summary>
    public int? NewSectionParentId { get; set; }

    /// <summary>
    /// Gets or sets the name of the new child section to create on commit.
    /// </summary>
    public string? NewSectionName { get; set; }

    /// <summary>
    /// Gets or sets the plan name. Defaults to <c>[AB#1234] Story Title</c>.
    /// </summary>
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description for the new plan (exposed in the full-edit draft page).
    /// </summary>
    public string? PlanDescription { get; set; }

    /// <summary>
    /// Gets or sets the name of the single test run created inside the new plan. Defaults to <c>Run 1</c>.
    /// </summary>
    public string RunName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description for the run (exposed in the full-edit draft page).
    /// </summary>
    public string? RunDescription { get; set; }

    /// <summary>
    /// Gets or sets the optional user identifier this run is assigned to.
    /// </summary>
    public int? RunAssignedToId { get; set; }

    /// <summary>
    /// Gets or sets the editable list of case drafts derived from the story's acceptance criteria.
    /// </summary>
    public List<CaseDraft> Cases { get; set; } = [];

    /// <summary>
    /// Gets or sets the AC parser diagnostics reported on the review page
    /// ("Detected AC format: Numbered list — 6 items"). Set by Load, carried for display only.
    /// </summary>
    public string? DetectedAcFormat { get; set; }

    /// <summary>
    /// Gets or sets the number of AC items detected on Load (informational).
    /// </summary>
    public int? DetectedAcItemCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has acknowledged duplicate-title
    /// warnings and wants to proceed anyway. Must be explicitly ticked after a duplicate check
    /// finds matches — otherwise Confirm aborts before writing.
    /// </summary>
    public bool OverrideDuplicates { get; set; }
}
