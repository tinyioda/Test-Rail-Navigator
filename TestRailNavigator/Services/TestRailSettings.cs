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
    /// Gets or sets the username required to access the Setup page.
    /// When empty the Setup page is unprotected.
    /// </summary>
    public string SetupUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password required to access the Setup page.
    /// When empty the Setup page is unprotected.
    /// </summary>
    public string SetupPassword { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the console window is shown. Defaults to true.
    /// </summary>
    public bool ShowConsole { get; set; } = true;

    /// <summary>
    /// Gets or sets the encryption password for the local SQLite database.
    /// </summary>
    public string DatabasePassword { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure DevOps personal access token (PAT) used to read work items
    /// when generating TestRail test cases from Azure DevOps links.
    /// </summary>
    public string AzureDevOpsPat { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jira base URL (e.g. <c>https://your-tenant.atlassian.net</c>).
    /// Reserved for the future Jira integration; currently unused.
    /// </summary>
    public string JiraBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jira account email used with the API token.
    /// Reserved for the future Jira integration; currently unused.
    /// </summary>
    public string JiraEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jira API token.
    /// Reserved for the future Jira integration; currently unused.
    /// </summary>
    public string JiraApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base URL of an OpenAI-compatible endpoint used to enrich parsed ACs into
    /// fully-authored TestRail cases (structured steps, preconditions, summary). Supported
    /// backends: OpenAI (<c>https://api.openai.com/v1</c>), Azure OpenAI
    /// (<c>https://{resource}.openai.azure.com/openai/deployments/{deployment}</c>) and local
    /// Ollama (<c>http://localhost:11434/v1</c>). When empty, the "Enrich with AI" button is
    /// disabled and the GenerateCases page operates as a plain parser/scaffold.
    /// </summary>
    public string OpenAiEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key (or Azure OpenAI key) sent as <c>Authorization: Bearer …</c>.
    /// Leave blank for Ollama or other keyless endpoints.
    /// </summary>
    public string OpenAiApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model name to use for chat completions
    /// (e.g. <c>gpt-4o-mini</c>, <c>gpt-4.1</c>, or an Azure deployment ID).
    /// </summary>
    public string OpenAiModel { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Gets or sets the API version for Azure OpenAI (e.g. <c>2024-10-21</c>).
    /// Ignored for non-Azure endpoints.
    /// </summary>
    public string OpenAiApiVersion { get; set; } = string.Empty;
}
