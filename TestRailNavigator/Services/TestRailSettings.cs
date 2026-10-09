namespace TestRailNavigator.Services;

/// <summary>
/// Configuration settings for connecting to TestRail.
/// </summary>
public class TestRailSettings
{
    /// <summary>
    /// Gets or sets the base URL of the TestRail instance.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the username (email) for authentication.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key for authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether write operations (create, edit, delete) are enabled.
    /// When false the application operates in read-only mode. Defaults to false.
    /// </summary>
    public bool AllowWrites { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the console window is shown. Defaults to true.
    /// </summary>
    public bool ShowConsole { get; set; } = true;

    /// <summary>
    /// Gets or sets the encryption password for the local SQLite database.
    /// </summary>
    public string DatabasePassword { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the approved HTTPS Azure DevOps organization or collection URL.
    /// Work-item links outside this base are rejected before credentials are sent.
    /// Optional: sourced from the <c>AzureDevOps</c> section of <c>appsettings.json</c>. When
    /// empty (the default), the Azure DevOps-dependent features (Generate Cases, Generate
    /// Hierarchy, New Plan from Story) are disabled in the UI rather than erroring.
    /// </summary>
    public string AzureDevOpsBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure DevOps personal access token (PAT) used to read work items
    /// when generating TestRail test cases from Azure DevOps links. Optional; see
    /// <see cref="AzureDevOpsBaseUrl"/>.
    /// </summary>
    public string AzureDevOpsPat { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jira base URL (e.g. <c>https://your-tenant.atlassian.net</c>).
    /// Optional: sourced from the <c>Jira</c> section of <c>appsettings.json</c>. Reserved for
    /// the future Jira integration — currently unused by any feature.
    /// </summary>
    public string JiraBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jira account email used with the API token. Optional; see
    /// <see cref="JiraBaseUrl"/>.
    /// </summary>
    public string JiraEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jira API token. Optional; see <see cref="JiraBaseUrl"/>.
    /// </summary>
    public string JiraApiToken { get; set; } = string.Empty;
}
