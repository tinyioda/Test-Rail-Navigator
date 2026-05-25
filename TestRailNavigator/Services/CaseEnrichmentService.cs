using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TestRailNavigator.Models;

namespace TestRailNavigator.Services;

/// <summary>
/// Transforms a parser-generated <see cref="CaseDraft"/> (verbatim AC text scaffold) into a
/// fully-authored TestRail case: structured preconditions, one-paragraph summary, and separated
/// step/expected pairs. Modeled after the LLM-backed <c>testrail-author</c> agent but runs
/// in-process via an OpenAI-compatible chat-completions endpoint.
/// </summary>
public interface ICaseEnrichmentService
{
    /// <summary>
    /// Returns true when an OpenAI-compatible endpoint and model are configured in
    /// testrail-settings.json. Used by the UI to enable/disable the "Enrich with AI" button.
    /// </summary>
    Task<bool> IsConfiguredAsync();

    /// <summary>Gets a short display name for the configured endpoint (used in the UI banner).</summary>
    Task<string> GetDisplayNameAsync();

    /// <summary>
    /// Enriches a single case draft using the provided story context. On success, returns a
    /// new <see cref="CaseDraft"/> with <see cref="CaseDraft.Summary"/>, populated
    /// <see cref="CaseDraft.Preconditions"/>, and <see cref="CaseDraft.StepsSeparated"/>.
    /// </summary>
    /// <param name="draft">The parser-generated draft to enrich (title + raw AC body).</param>
    /// <param name="context">Story-level context (title, description, other ACs) for the prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CaseDraft> EnrichAsync(CaseDraft draft, StoryEnrichmentContext context, CancellationToken ct = default);
}

/// <summary>
/// Minimal story context passed to the LLM alongside the per-AC prompt.
/// </summary>
/// <param name="WorkItemId">The numeric AzDO work item id (e.g. 410080).</param>
/// <param name="WorkItemTitle">Work item title.</param>
/// <param name="WorkItemType">Work item type (e.g. "User Story", "Bug").</param>
/// <param name="DescriptionPlainText">The story's Description field, rendered as plain text.</param>
/// <param name="AllAcceptanceCriteriaPlainText">The full AC block, plain-text.</param>
public record StoryEnrichmentContext(
    int? WorkItemId,
    string WorkItemTitle,
    string WorkItemType,
    string DescriptionPlainText,
    string AllAcceptanceCriteriaPlainText);

/// <summary>
/// Calls an OpenAI-compatible <c>/v1/chat/completions</c> endpoint. Works for OpenAI,
/// Azure OpenAI (with API-version query string), and local Ollama.
/// </summary>
public class OpenAiCompatibleEnrichmentService : ICaseEnrichmentService
{
    private readonly HttpClient _http;
    private readonly SettingsService _settings;
    private readonly ConsoleLogService _console;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Initializes a new <see cref="OpenAiCompatibleEnrichmentService"/>.</summary>
    public OpenAiCompatibleEnrichmentService(HttpClient http, SettingsService settings, ConsoleLogService console)
    {
        _http = http;
        _settings = settings;
        _console = console;
    }

    /// <inheritdoc />
    public async Task<bool> IsConfiguredAsync()
    {
        var s = await _settings.GetSettingsAsync();
        return s is not null
               && !string.IsNullOrWhiteSpace(s.OpenAiEndpoint)
               && !string.IsNullOrWhiteSpace(s.OpenAiModel);
    }

    /// <inheritdoc />
    public async Task<string> GetDisplayNameAsync()
    {
        var s = await _settings.GetSettingsAsync();
        if (s is null || string.IsNullOrWhiteSpace(s.OpenAiEndpoint)) return "(not configured)";
        try { return $"{s.OpenAiModel} @ {new Uri(s.OpenAiEndpoint).Host}"; }
        catch { return s.OpenAiModel; }
    }

    /// <inheritdoc />
    public async Task<CaseDraft> EnrichAsync(CaseDraft draft, StoryEnrichmentContext context, CancellationToken ct = default)
    {
        var settings = await _settings.GetSettingsAsync();
        if (settings is null || string.IsNullOrWhiteSpace(settings.OpenAiEndpoint))
        {
            throw new InvalidOperationException("OpenAiEndpoint is not configured. Set it on the Setup page.");
        }

        var url = BuildChatUrl(settings);
        var payload = new ChatRequest
        {
            Model = settings.OpenAiModel,
            Temperature = 0.2,
            ResponseFormat = new ResponseFormat { Type = "json_object" },
            Messages =
            [
                new ChatMessage { Role = "system", Content = SystemPrompt },
                new ChatMessage { Role = "user", Content = BuildUserPrompt(draft, context) }
            ]
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        if (!string.IsNullOrWhiteSpace(settings.OpenAiApiKey))
        {
            req.Headers.Add("Authorization", $"Bearer {settings.OpenAiApiKey}");
            req.Headers.Add("api-key", settings.OpenAiApiKey);
        }

        var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _console.Log($"LLM enrichment HTTP {(int)res.StatusCode}: {Truncate(body, 500)}");
            throw new HttpRequestException($"LLM endpoint returned {(int)res.StatusCode}: {Truncate(body, 200)}");
        }

        var parsed = JsonSerializer.Deserialize<ChatResponse>(body, JsonOptions);
        var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("LLM returned an empty response.");
        }

        EnrichedCase payloadCase;
        try
        {
            payloadCase = JsonSerializer.Deserialize<EnrichedCase>(content, JsonOptions)
                ?? throw new InvalidOperationException("LLM JSON could not be deserialized.");
        }
        catch (JsonException ex)
        {
            _console.Log($"LLM JSON parse failure: {ex.Message}. Raw: {Truncate(content, 500)}");
            throw new InvalidOperationException("LLM did not return valid JSON. See console log for details.", ex);
        }

        return new CaseDraft
        {
            Title = string.IsNullOrWhiteSpace(payloadCase.Title) ? draft.Title : payloadCase.Title,
            Summary = payloadCase.Summary,
            Preconditions = payloadCase.Preconditions ?? draft.Preconditions,
            StepsSeparated = payloadCase.Steps?
                .Where(s => !string.IsNullOrWhiteSpace(s.Content))
                .Select(s => new Models.TestCaseStep { Content = s.Content, Expected = s.Expected ?? string.Empty })
                .ToList(),
            Steps = draft.Steps,
            Expected = draft.Expected,
            Include = draft.Include,
            Refs = draft.Refs,
            TypeId = draft.TypeId,
            PriorityId = draft.PriorityId,
            TemplateId = 2, // Test Case (Steps) — matches hand-authored cases
            Estimate = draft.Estimate,
            OriginalTitle = draft.OriginalTitle,
            OriginalSteps = draft.OriginalSteps,
            OriginalExpected = draft.OriginalExpected,
            OriginalPreconditions = draft.OriginalPreconditions,
            Enriched = true
        };
    }

    private static string BuildChatUrl(TestRailSettings s)
    {
        var endpoint = s.OpenAiEndpoint.TrimEnd('/');
        // Azure OpenAI: endpoint already points at a deployment. Append /chat/completions + api-version.
        if (endpoint.Contains(".openai.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            var ver = string.IsNullOrWhiteSpace(s.OpenAiApiVersion) ? "2024-10-21" : s.OpenAiApiVersion;
            return $"{endpoint}/chat/completions?api-version={ver}";
        }
        // OpenAI / Ollama / any /v1-style server.
        return endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? endpoint
            : $"{endpoint}/chat/completions";
    }

    private const string SystemPrompt = """
        You are a senior QA engineer authoring a single TestRail test case from one Acceptance
        Criterion of an Azure DevOps work item. You transform the verbatim AC text into a concrete,
        executable test case that a junior tester could run without asking questions.

        Always respond with a single JSON object matching this schema (no markdown, no prose):
        {
          "title":   "<ACn — Short Title: Verb-first descriptor>  (<= 250 chars, keep the ACn prefix from the input)",
          "summary": "<one-paragraph plain-language description of what this test verifies>",
          "preconditions": "<multi-line setup requirements. Use '- ' bullets. Reference specific tools, URLs, accounts, files.>",
          "steps": [
            { "content": "<concrete action the tester performs, imperative voice>", "expected": "<observable outcome>" },
            ...
          ]
        }

        Rules:
        - Produce between 3 and 8 steps. Each step must be independently verifiable.
        - Each step's "expected" MUST be a specific observable outcome, not a restatement of the action.
        - Keep the "ACn — " prefix exactly as it appears in the input title.
        - If the AC mentions specific paths, URLs, file names, tool versions, or numbers, preserve them verbatim in steps.
        - Do NOT wrap the JSON in markdown fences. Do NOT add commentary before or after the JSON.
        """;

    private static string BuildUserPrompt(CaseDraft draft, StoryEnrichmentContext ctx)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Work Item Context:");
        sb.AppendLine($"- Type: {ctx.WorkItemType}");
        sb.AppendLine($"- Id:   {(ctx.WorkItemId?.ToString() ?? "(unknown)")}");
        sb.AppendLine($"- Title: {ctx.WorkItemTitle}");
        if (!string.IsNullOrWhiteSpace(ctx.DescriptionPlainText))
        {
            sb.AppendLine();
            sb.AppendLine("Story Description:");
            sb.AppendLine(Truncate(ctx.DescriptionPlainText, 2000));
        }
        if (!string.IsNullOrWhiteSpace(ctx.AllAcceptanceCriteriaPlainText))
        {
            sb.AppendLine();
            sb.AppendLine("All Acceptance Criteria (for context only — author the test for THIS ONE):");
            sb.AppendLine(Truncate(ctx.AllAcceptanceCriteriaPlainText, 3000));
        }
        sb.AppendLine();
        sb.AppendLine("Author a TestRail case for this specific Acceptance Criterion:");
        sb.AppendLine($"Title: {draft.Title}");
        if (!string.IsNullOrWhiteSpace(draft.Steps))
        {
            sb.AppendLine("Body:");
            sb.AppendLine(draft.Steps);
        }
        return sb.ToString();
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + " …";

    private class ChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; } = [];
        [JsonPropertyName("temperature")] public double Temperature { get; set; } = 0.2;
        [JsonPropertyName("response_format")] public ResponseFormat? ResponseFormat { get; set; }
    }

    private class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    }

    private class ResponseFormat
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "json_object";
    }

    private class ChatResponse
    {
        [JsonPropertyName("choices")] public List<ChatChoice>? Choices { get; set; }
    }

    private class ChatChoice
    {
        [JsonPropertyName("message")] public ChatMessage? Message { get; set; }
    }

    private class EnrichedCase
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("summary")] public string? Summary { get; set; }
        [JsonPropertyName("preconditions")] public string? Preconditions { get; set; }
        [JsonPropertyName("steps")] public List<EnrichedStep>? Steps { get; set; }
    }

    private class EnrichedStep
    {
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
        [JsonPropertyName("expected")] public string? Expected { get; set; }
    }
}
