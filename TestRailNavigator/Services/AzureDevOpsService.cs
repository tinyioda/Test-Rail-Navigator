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

    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDevOpsService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used for outbound REST calls, with automatic redirects disabled.</param>
    /// <param name="settingsService">The settings service providing the approved base URL and PAT.</param>
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
        return !string.IsNullOrWhiteSpace(settings?.AzureDevOpsPat) &&
            AzureDevOpsUrlPolicy.TryGetBaseUri(settings.AzureDevOpsBaseUrl, out _);
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
    /// <remarks>Recognizes URL syntax only. Requests are separately validated against the configured approved base URL.</remarks>
    public static bool TryParseWorkItemUrl(string url, out string organization, out string project, out int id)
        => AzureDevOpsUrlPolicy.TryParseWorkItemUrl(url, out organization, out project, out id);

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
        var (baseUri, pat) = await GetConfigurationAsync();
        var (project, id) = AzureDevOpsUrlPolicy.ParseWorkItemUrl(baseUri, url);
        var root = await FetchWorkItemJsonAsync(baseUri, project, id, pat);
        return BuildItem(root, baseUri, project, id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IssueTrackerItem>> GetChildrenAsync(IssueTrackerItem item)
    {
        if (item.ChildUrls.Count == 0)
        {
            return [];
        }

        var (baseUri, pat) = await GetConfigurationAsync();
        var (parentProject, _) = AzureDevOpsUrlPolicy.ParseWorkItemUrl(baseUri, item.SourceUrl);
        var children = item.ChildUrls
            .Select(url => AzureDevOpsUrlPolicy.ParseWorkItemUrl(baseUri, url, parentProject))
            .ToArray();

        var results = new List<IssueTrackerItem>(children.Length);
        foreach (var (project, id) in children)
        {
            var root = await FetchWorkItemJsonAsync(baseUri, project, id, pat);
            results.Add(BuildItem(root, baseUri, project, id));
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

    private async Task<(Uri BaseUri, string Pat)> GetConfigurationAsync()
    {
        var settings = await _settingsService.GetSettingsAsync()
            ?? throw new InvalidOperationException("TestRail settings not configured");

        if (!AzureDevOpsUrlPolicy.TryGetBaseUri(settings.AzureDevOpsBaseUrl, out var baseUri))
        {
            throw new InvalidOperationException(
                "Azure DevOps base URL is not configured or is invalid. Set an approved HTTPS organization or collection URL on the Setup page.");
        }

        if (string.IsNullOrWhiteSpace(settings.AzureDevOpsPat))
        {
            throw new InvalidOperationException(
                "Azure DevOps PAT is not configured. Add it on the Setup page to generate cases from Azure DevOps links.");
        }

        return (baseUri, settings.AzureDevOpsPat);
    }

    private async Task<JsonElement> FetchWorkItemJsonAsync(Uri baseUri, string project, int id, string pat)
    {
        // $expand=relations gives us hierarchy links for child discovery.
        var requestUrl =
            $"{baseUri.AbsoluteUri.TrimEnd('/')}/{Uri.EscapeDataString(project)}" +
            $"/_apis/wit/workitems/{id.ToString(System.Globalization.CultureInfo.InvariantCulture)}?$expand=relations&api-version={ApiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await _httpClient.SendAsync(request);
        if ((int)response.StatusCode is >= 300 and < 400)
        {
            throw new HttpRequestException(
                "Azure DevOps returned a redirect. Redirects are not allowed for authenticated Azure DevOps requests.",
                null,
                response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            string? errorMessage = null;
            try
            {
                using var errorDoc = JsonDocument.Parse(body);
                if (errorDoc.RootElement.ValueKind == JsonValueKind.Object &&
                    errorDoc.RootElement.TryGetProperty("message", out var msg) &&
                    msg.ValueKind == JsonValueKind.String)
                {
                    errorMessage = msg.GetString();
                }
            }
            catch (JsonException)
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

    private static string BuildWebUrl(Uri baseUri, string project, int id) =>
        $"{baseUri.AbsoluteUri.TrimEnd('/')}/{Uri.EscapeDataString(project)}" +
        $"/_workitems/edit/{id.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    private static IssueTrackerItem BuildItem(JsonElement root, Uri baseUri, string project, int id)
    {
        var item = new IssueTrackerItem
        {
            Id = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            NumericId = id,
            Reference = $"AB#{id}",
            SourceUrl = BuildWebUrl(baseUri, project, id)
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
                var (childProject, childId) = AzureDevOpsUrlPolicy.ParseWorkItemUrl(baseUri, apiUrl, project);
                item.ChildUrls.Add(BuildWebUrl(baseUri, childProject, childId));
            }
        }

        return item;
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
