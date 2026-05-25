namespace TestRailNavigator.Models;

/// <summary>
/// Represents a subset of the fields returned by the Azure DevOps Work Item REST API,
/// used when generating TestRail test cases from Azure DevOps links.
/// </summary>
public class AzureDevOpsWorkItem
{
    /// <summary>Gets or sets the Azure DevOps work item identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the work item type (e.g. "User Story", "Bug", "Test Case").</summary>
    public string WorkItemType { get; set; } = string.Empty;

    /// <summary>Gets or sets the work item title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the HTML description, if any.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the HTML acceptance criteria, if any.</summary>
    public string? AcceptanceCriteria { get; set; }

    /// <summary>Gets or sets the HTML repro steps, if any (typically for Bug work items).</summary>
    public string? ReproSteps { get; set; }

    /// <summary>Gets or sets the original Azure DevOps work item URL used to locate this item.</summary>
    public string SourceUrl { get; set; } = string.Empty;
}
