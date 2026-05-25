using TestRailNavigator.Models;

namespace TestRailNavigator.Services;

/// <summary>
/// Abstraction over an external issue tracker (Azure DevOps today, Jira planned),
/// allowing the hierarchy generator to walk work items without coupling to a specific vendor.
/// </summary>
public interface IIssueTrackerClient
{
    /// <summary>Gets the display name of the tracker (e.g. "Azure DevOps", "Jira").</summary>
    string DisplayName { get; }

    /// <summary>
    /// Determines whether this client recognizes the supplied URL and can fetch it.
    /// </summary>
    bool CanHandle(string url);

    /// <summary>
    /// Fetches a single work item by URL.
    /// </summary>
    /// <returns>The populated item, or <see langword="null"/> when the URL is not recognized.</returns>
    Task<IssueTrackerItem?> GetItemAsync(string url);

    /// <summary>
    /// Fetches the direct hierarchy children of the supplied item (Epic → Features, Feature → Stories, etc.).
    /// </summary>
    /// <param name="item">The parent item.</param>
    /// <returns>Child work items in tracker-defined order. Returns an empty list when the item has no children.</returns>
    Task<IReadOnlyList<IssueTrackerItem>> GetChildrenAsync(IssueTrackerItem item);

    /// <summary>
    /// Indicates whether the tracker has the required credentials configured.
    /// </summary>
    Task<bool> IsConfiguredAsync();
}
