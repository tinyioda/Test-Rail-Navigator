using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TestRailNavigator.Models;

namespace TestRailNavigator.Services;

/// <summary>
/// Client for reading Azure DevOps work items. Used by the single-case generator and by the
/// hierarchy generator (which walks Epic → Feature → Story via <c>System.LinkTypes.Hierarchy-Forward</c>).
/// </summary>
public class AzureDevOpsService : IIssueTrackerClient
{
    private const string ApiVersion = "7.1";

    // Hosted Azure DevOps: https://dev.azure.com/{org}/{project}/_workitems/edit/{id}
    private static readonly Regex DevAzureUrlRegex = new(
        @"^(?<base>https?://dev\.azure\.com/(?<org>[^/]+))/(?<project>[^/]+)/_workitems/(?:edit|view)/(?<id>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Legacy: https://{org}.visualstudio.com/{project}/_workitems/edit/{id}
    private static readonly Regex VisualStudioUrlRegex = new(
        @"^(?<base>https?://(?<org>[^.]+)\.visualstudio\.com)/(?<project>[^/]+)/_workitems/(?:edit|view)/(?<id>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // On-prem TFS / Azure DevOps Server: https://{host}[:port]/[path/]{collection}/{project}/_workitems/edit/{id}
    // The {base} group captures everything up to but not including the project segment.
    private static readonly Regex TfsUrlRegex = new(
        @"^(?<base>https?://[^/]+(?:/[^/]+)*)/(?<project>[^/]+)/_workitems/(?:edit|view)/(?<id>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // API URLs for the same three shapes.
    private static readonly Regex DevAzureApiRegex = new(
        @"^(?<base>https?://dev\.azure\.com/(?<org>[^/]+))/(?<project>[^/]+)/_apis/wit/workItems/(?<id>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GenericApiRegex = new(
        @"^(?<base>https?://[^/]+(?:/[^/]+)*?)/(?:(?<project>[^/]+)/)?_apis/wit/workItems/(?<id>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDevOpsService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used for outbound REST calls.</param>
    /// <param name="settingsService">The settings service providing the configured PAT.</param>
    public AzureDevOpsService(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <inheritdoc />
    public string DisplayName => "Azure DevOps";

    /// <inheritdoc />
    public bool CanHandle(string url) => TryParseWorkItemUrl(url, out _, out _, out _);

    /// <inheritdoc />
    public async Task<bool> IsConfiguredAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        return !string.IsNullOrWhiteSpace(settings?.AzureDevOpsPat);
    }

    /// <summary>
    /// Attempts to parse an Azure DevOps / TFS work item URL.
    /// Supports <c>dev.azure.com</c>, legacy <c>{org}.visualstudio.com</c>, and on-prem
    /// TFS / Azure DevOps Server (e.g. <c>https://tfs.example.com/tfs/DefaultCollection/Proj/_workitems/edit/123</c>).
    /// </summary>
    /// <param name="url">The URL to parse.</param>
    /// <param name="organization">For hosted AzDO this is the org; for on-prem it's the collection segment.</param>
    /// <param name="project">The team project name.</param>
    /// <param name="id">The numeric work item id.</param>
    public static bool TryParseWorkItemUrl(string url, out string organization, out string project, out int id)
        => TryParseCore(url, out _, out organization, out project, out id);

    private static bool TryParseCore(
        string url,
        out string baseUrl,
        out string organization,
        out string project,
        out int id)
    {
        baseUrl = string.Empty;
        organization = string.Empty;
        project = string.Empty;
        id = 0;

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var trimmed = url.Trim();

        // Try hosted AzDO first, then legacy visualstudio.com, then generic (TFS on-prem) as the
        // broad fallback. The hosted regexes are strict enough that they don't double-match here.
        var match = DevAzureUrlRegex.Match(trimmed);
        if (!match.Success) match = VisualStudioUrlRegex.Match(trimmed);
        if (!match.Success) match = DevAzureApiRegex.Match(trimmed);
        if (!match.Success) match = TfsUrlRegex.Match(trimmed);

        if (!match.Success || !int.TryParse(match.Groups["id"].Value, out id))
        {
            return false;
        }

        baseUrl = match.Groups["base"].Value.TrimEnd('/');
        project = Uri.UnescapeDataString(match.Groups["project"].Value);

        // `organization` is informational for consumers; for TFS we surface the last path segment
        // of the base URL (typically the collection name, e.g. "DefaultCollection").
        if (match.Groups["org"].Success)
        {
            organization = Uri.UnescapeDataString(match.Groups["org"].Value);
        }
        else
        {
            var lastSlash = baseUrl.LastIndexOf('/');
            organization = lastSlash >= 0 && lastSlash < baseUrl.Length - 1
                ? Uri.UnescapeDataString(baseUrl[(lastSlash + 1)..])
                : string.Empty;
        }

        return true;
    }

    /// <summary>
    /// Backwards-compatible helper used by the simple <c>GenerateCases</c> page.
    /// </summary>
    public async Task<AzureDevOpsWorkItem?> GetWorkItemAsync(string url)
    {
        var item = await GetItemAsync(url);
        if (item is null) return null;

        return new AzureDevOpsWorkItem
        {
            Id = item.NumericId ?? 0,
            Title = item.Title,
            WorkItemType = item.RawType,
            Description = item.DescriptionHtml,
            AcceptanceCriteria = item.AcceptanceCriteriaHtml,
            ReproSteps = item.ReproStepsHtml,
            SourceUrl = item.SourceUrl
        };
    }

    /// <inheritdoc />
    public async Task<IssueTrackerItem?> GetItemAsync(string url)
    {
        if (!TryParseCore(url, out var baseUrl, out _, out var project, out var id))
        {
            return null;
        }

        var root = await FetchWorkItemJsonAsync(baseUrl, project, id);
        return BuildItem(root, baseUrl, project, id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IssueTrackerItem>> GetChildrenAsync(IssueTrackerItem item)
    {
        if (item.ChildUrls.Count == 0)
        {
            return [];
        }

        var results = new List<IssueTrackerItem>(item.ChildUrls.Count);
        foreach (var childUrl in item.ChildUrls)
        {
            try
            {
                var child = await GetItemAsync(childUrl);
                if (child is not null)
                {
                    results.Add(child);
                }
            }
            catch
            {
                // Swallow per-child failures so one broken link doesn't break the entire walk.
                // The orchestrator surfaces a warning when the expected hierarchy is incomplete.
            }
        }

        return results;
    }

    /// <summary>
    /// Converts an HTML fragment into a plain-text representation suitable for TestRail multi-line text fields.
    /// </summary>
    public static string? HtmlToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var normalized = Regex.Replace(html, @"<\s*(br|/p|/div|/li|/tr)\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<\s*li[^>]*>", "- ", RegexOptions.IgnoreCase);

        var text = HtmlTagRegex.Replace(normalized, string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text);

        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private async Task<JsonElement> FetchWorkItemJsonAsync(string baseUrl, string project, int id)
    {
        var settings = await _settingsService.GetSettingsAsync()
            ?? throw new InvalidOperationException("TestRail settings not configured");

        if (string.IsNullOrWhiteSpace(settings.AzureDevOpsPat))
        {
            throw new InvalidOperationException(
                "Azure DevOps PAT is not configured. Add it on the Setup page to generate cases from Azure DevOps links.");
        }

        // $expand=relations gives us hierarchy links for child discovery.
        var requestUrl =
            $"{baseUrl}/{Uri.EscapeDataString(project)}" +
            $"/_apis/wit/workitems/{id}?$expand=relations&api-version={ApiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{settings.AzureDevOpsPat}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            string? errorMessage = null;
            try
            {
                using var errorDoc = JsonDocument.Parse(body);
                if (errorDoc.RootElement.TryGetProperty("message", out var msg))
                {
                    errorMessage = msg.GetString();
                }
            }
            catch
            {
                // Response body was not JSON.
            }

            throw new HttpRequestException(
                errorMessage ?? $"Azure DevOps API returned {(int)response.StatusCode} {response.ReasonPhrase}",
                null,
                response.StatusCode);
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static IssueTrackerItem BuildItem(JsonElement root, string baseUrl, string project, int id)
    {
        var webUrl =
            $"{baseUrl}/{Uri.EscapeDataString(project)}" +
            $"/_workitems/edit/{id}";

        var item = new IssueTrackerItem
        {
            Id = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            NumericId = id,
            Reference = $"AB#{id}",
            SourceUrl = webUrl
        };

        if (root.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object)
        {
            item.Title = ReadString(fields, "System.Title") ?? string.Empty;
            item.RawType = ReadString(fields, "System.WorkItemType") ?? string.Empty;
            item.Kind = ClassifyKind(item.RawType);
            item.DescriptionHtml = ReadString(fields, "System.Description");
            item.AcceptanceCriteriaHtml = ReadString(fields, "Microsoft.VSTS.Common.AcceptanceCriteria");
            item.ReproStepsHtml = ReadString(fields, "Microsoft.VSTS.TCM.ReproSteps");
        }

        if (root.TryGetProperty("relations", out var relations) && relations.ValueKind == JsonValueKind.Array)
        {
            foreach (var relation in relations.EnumerateArray())
            {
                var rel = relation.TryGetProperty("rel", out var r) ? r.GetString() : null;
                if (!string.Equals(rel, "System.LinkTypes.Hierarchy-Forward", StringComparison.Ordinal))
                {
                    continue;
                }

                var apiUrl = relation.TryGetProperty("url", out var u) ? u.GetString() : null;
                if (string.IsNullOrWhiteSpace(apiUrl)) continue;

                if (TryConvertApiUrlToWebUrl(apiUrl, baseUrl, project, out var webChildUrl))
                {
                    item.ChildUrls.Add(webChildUrl);
                }
            }
        }

        return item;
    }

    private static bool TryConvertApiUrlToWebUrl(string apiUrl, string parentBaseUrl, string fallbackProject, out string webUrl)
    {
        webUrl = string.Empty;

        // Hosted dev.azure.com API URL: full base + project are present.
        var match = DevAzureApiRegex.Match(apiUrl);
        if (match.Success)
        {
            var childBase = match.Groups["base"].Value.TrimEnd('/');
            var proj = Uri.UnescapeDataString(match.Groups["project"].Value);
            webUrl = $"{childBase}/{Uri.EscapeDataString(proj)}/_workitems/edit/{match.Groups["id"].Value}";
            return true;
        }

        // Generic (hosted org-scoped relation URL or TFS on-prem). Relation URLs on TFS are
        // usually of the form {base}/_apis/wit/workItems/{id} with no project segment — fall back
        // to the parent's project and base so the web URL is reachable.
        var generic = GenericApiRegex.Match(apiUrl);
        if (generic.Success)
        {
            var childBase = generic.Groups["base"].Value.TrimEnd('/');
            var proj = generic.Groups["project"].Success && !string.IsNullOrEmpty(generic.Groups["project"].Value)
                ? Uri.UnescapeDataString(generic.Groups["project"].Value)
                : fallbackProject;

            // If the generic match dropped back to just the host (e.g. TFS relation URL that ends
            // at `/_apis/...` right after the collection), prefer the parent's known base URL
            // because it may include additional path segments (`/tfs/DefaultCollection`) that
            // the non-greedy regex trimmed off.
            if (!string.IsNullOrEmpty(parentBaseUrl) &&
                Uri.TryCreate(parentBaseUrl, UriKind.Absolute, out var parentUri) &&
                Uri.TryCreate(childBase, UriKind.Absolute, out var childUri) &&
                string.Equals(parentUri.Host, childUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                childBase = parentBaseUrl.TrimEnd('/');
            }

            webUrl = $"{childBase}/{Uri.EscapeDataString(proj)}/_workitems/edit/{generic.Groups["id"].Value}";
            return true;
        }

        return false;
    }

    private static IssueTrackerItemKind ClassifyKind(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return IssueTrackerItemKind.Unknown;

        return rawType.Trim().ToLowerInvariant() switch
        {
            "epic" => IssueTrackerItemKind.Epic,
            "feature" => IssueTrackerItemKind.Feature,
            "user story" or "product backlog item" or "requirement" or "story" => IssueTrackerItemKind.Story,
            "bug" => IssueTrackerItemKind.Bug,
            "task" => IssueTrackerItemKind.Task,
            "test case" => IssueTrackerItemKind.TestCase,
            _ => IssueTrackerItemKind.Unknown
        };
    }

    private static string? ReadString(JsonElement fields, string name)
    {
        if (fields.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}
