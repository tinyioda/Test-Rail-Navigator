using System.Text.RegularExpressions;

namespace TestRailNavigator.Services;

/// <summary>
/// Parses free-form acceptance criteria text from an issue tracker into discrete TestRail case candidates.
/// </summary>
/// <remarks>
/// Detection strategy, in order (first strategy to yield ≥1 candidate wins):
/// <list type="number">
///   <item>Strip a leading <c>Acceptance Criteria</c> heading if present and recurse on the remainder.</item>
///   <item>Gherkin blocks (<c>Given/When/Then</c> — with or without <c>Scenario:</c> headers).</item>
///   <item>Short <c>Scenario:</c> / <c>Example:</c> headers without full Gherkin body.</item>
///   <item>Tabular AC (<c>&lt;table&gt;</c> with headers like <c>AC</c>, <c>Given</c>, <c>When</c>, <c>Then</c>).</item>
///   <item>HTML list items (<c>&lt;li&gt;</c>).</item>
///   <item>Numbered lines (<c>1.</c>, <c>1)</c>).</item>
///   <item>AC-prefixed lines (<c>AC1:</c>, <c>Criterion 2 —</c>).</item>
///   <item>Checklist items (<c>[ ]</c>, <c>[x]</c>, <c>☐</c>, <c>✔</c>).</item>
///   <item>Rule / Example blocks (Gojko Adzic style).</item>
///   <item>"shall / should / must" requirement sentences.</item>
///   <item>Paragraph fallback (split on blank lines).</item>
/// </list>
/// When nothing can be extracted a single fallback candidate is returned so the caller still
/// produces at least one case per work item.
/// </remarks>
public static class AcceptanceCriteriaParser
{
    private static readonly Regex GherkinHeaderRegex = new(
        @"^\s*(?<kw>Given|When|Then|And|But)\b",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex ListItemRegex = new(
        @"<li[^>]*>(?<body>.*?)</li>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TableRowRegex = new(
        @"<tr[^>]*>(?<body>.*?)</tr>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TableCellRegex = new(
        @"<t[dh][^>]*>(?<body>.*?)</t[dh]>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex HeadingStripRegex = new(
        @"^\s*(?:<(?:h\d|p|strong|b)[^>]*>\s*)?\**\s*Acceptance\s+Criteria\s*:?\s*\**\s*(?:</(?:h\d|p|strong|b)>)?\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NumberedLineRegex = new(
        @"^\s*\d+[.)]\s+\S",
        RegexOptions.Compiled);

    private static readonly Regex AcPrefixRegex = new(
        @"^\s*(?:AC|Criterion|Crit)[\s\-_]?\d+\s*[:.\-–—]\s*\S",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ChecklistLineRegex = new(
        @"^\s*(?:\[\s?[xX✓]?\s?\]|☐|☑|✅|✔)\s*\S",
        RegexOptions.Compiled);

    private static readonly Regex ScenarioHeaderRegex = new(
        @"^\s*(?:Scenario(?:\s+Outline)?|Example)\s*:\s*(?<title>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RuleOrExampleLineRegex = new(
        @"^\s*(?<kw>Rule|Example)\s*:\s*(?<body>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ShallShouldMustRegex = new(
        @"\b(?:shall|should|must)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Marker that separates the "steps / when" portion of an AC body from the
    /// "expected results" portion. Matches variants like <c>Expected Results:</c>,
    /// <c>Expected Result:</c>, <c>Expected:</c>, and <c>Then:</c> at the start of a line
    /// (after plain-text rendering, so inline <c>&lt;u&gt;</c>/<c>&lt;b&gt;</c> tags are
    /// already stripped). The match consumes the marker and any trailing whitespace so the
    /// caller can keep the remainder verbatim.
    /// </summary>
    private static readonly Regex ExpectedResultsMarkerRegex = new(
        @"(?m)^\s*(?:Expected(?:\s+Result(?:s)?)?|Then)\s*:\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SentenceSplitRegex = new(
        @"(?<=[.!?])\s+(?=[A-Z])",
        RegexOptions.Compiled);

    /// <summary>
    /// A single acceptance-criterion-derived case candidate.
    /// </summary>
    /// <param name="Title">The short title (truncated to 250 chars for TestRail).</param>
    /// <param name="Steps">The steps body, or <see langword="null"/>.</param>
    /// <param name="Expected">The expected result body, or <see langword="null"/>.</param>
    public record CaseCandidate(string Title, string? Steps, string? Expected);

    /// <summary>
    /// Diagnostics describing which parsing strategy produced the case list.
    /// </summary>
    /// <param name="DetectedFormat">Human-readable name of the format that matched.</param>
    /// <param name="ItemCount">Number of candidates produced.</param>
    /// <param name="Warnings">Any non-fatal parsing warnings (may be empty).</param>
    public record ParserDiagnostics(string DetectedFormat, int ItemCount, IReadOnlyList<string> Warnings);

    /// <summary>
    /// Backwards-compatible entry point used by <c>GenerateCases</c> and <c>HierarchyGenerator</c>.
    /// </summary>
    public static IReadOnlyList<CaseCandidate> Parse(string? html, string fallbackTitle)
    {
        return ParseWithDiagnostics(html, fallbackTitle).Candidates;
    }

    /// <summary>
    /// Parses the supplied AC HTML and additionally returns a <see cref="ParserDiagnostics"/>
    /// describing the strategy that won. Used by the "Create Plan from Story" wizard to
    /// tell the user why the generated rows look the way they do.
    /// </summary>
    public static ParseResult ParseWithDiagnostics(string? html, string fallbackTitle)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            var fallback = new List<CaseCandidate> { new(Truncate(fallbackTitle, 250), null, null) };
            return new ParseResult(fallback, new ParserDiagnostics("Empty (fallback)", 1, []));
        }

        // 0. Strip "Acceptance Criteria" heading if present and recurse on the remainder.
        var stripped = HeadingStripRegex.Replace(html, string.Empty, 1);
        if (!ReferenceEquals(stripped, html) && stripped.Length != html.Length)
        {
            html = stripped;
        }

        var plainText = AzureDevOpsService.HtmlToPlainText(html) ?? string.Empty;

        // 0a. Explicit "ACn —/:/.-" prefixed items beat anything else, even when the body
        // happens to use Gherkin keywords like "When ...". Otherwise multi-AC stories where
        // every criterion starts with "When ..." get collapsed into a single mega-case. We keep
        // the "ACn — " prefix in the title so the generated TestRail case matches the naming
        // convention used by hand-authored cases.
        var earlyAcPrefixed = ParseLinePatternWithBody(plainText, AcPrefixRegex, keepPrefixInTitle: true);
        if (earlyAcPrefixed.Count > 0)
        {
            return new ParseResult(earlyAcPrefixed, new ParserDiagnostics("AC-prefixed items", earlyAcPrefixed.Count, []));
        }

        // 1. Gherkin.
        if (GherkinHeaderRegex.IsMatch(plainText))
        {
            var gherkin = ParseGherkin(plainText, fallbackTitle);
            if (gherkin.Count > 0)
            {
                return new ParseResult(gherkin, new ParserDiagnostics("Gherkin (Given/When/Then)", gherkin.Count, []));
            }
        }

        // 2. Short Scenario: / Example: headers without Gherkin body.
        var scenarioCandidates = ParseScenarioHeaders(plainText);
        if (scenarioCandidates.Count > 0)
        {
            return new ParseResult(scenarioCandidates, new ParserDiagnostics("Scenario headers", scenarioCandidates.Count, []));
        }

        // 3. Tabular AC.
        var tableCandidates = ParseTable(html);
        if (tableCandidates.Count > 0)
        {
            return new ParseResult(tableCandidates, new ParserDiagnostics("Tabular", tableCandidates.Count, []));
        }

        // 4. HTML list items.
        var listCandidates = ParseListItems(html);
        if (listCandidates.Count > 0)
        {
            return new ParseResult(listCandidates, new ParserDiagnostics("HTML list", listCandidates.Count, []));
        }

        // 5. Numbered list.
        var numbered = ParseLinePatternWithBody(plainText, NumberedLineRegex);
        if (numbered.Count >= 2)
        {
            return new ParseResult(numbered, new ParserDiagnostics("Numbered list", numbered.Count, []));
        }

        // 6. AC-prefixed (kept as a redundant later check so re-ordering above can be reverted
        // safely; ParseLinePatternWithBody is idempotent on already-grouped input).
        var acPrefixed = ParseLinePatternWithBody(plainText, AcPrefixRegex, keepPrefixInTitle: true);
        if (acPrefixed.Count > 0)
        {
            return new ParseResult(acPrefixed, new ParserDiagnostics("AC-prefixed items", acPrefixed.Count, []));
        }

        // 7. Checklist.
        var checklist = ParseLinePattern(plainText, ChecklistLineRegex);
        if (checklist.Count > 0)
        {
            return new ParseResult(checklist, new ParserDiagnostics("Checklist", checklist.Count, []));
        }

        // 8. Rule / Example blocks.
        var ruleExample = ParseRuleExample(plainText);
        if (ruleExample.Count > 0)
        {
            return new ParseResult(ruleExample, new ParserDiagnostics("Rule / Example", ruleExample.Count, []));
        }

        // 9. "shall / should / must" sentence extraction.
        var requirements = ParseRequirementSentences(plainText);
        if (requirements.Count >= 2)
        {
            return new ParseResult(requirements, new ParserDiagnostics("Requirement sentences", requirements.Count, []));
        }

        // 10. Paragraph fallback.
        var paragraphs = plainText
            .Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (paragraphs.Count > 0)
        {
            var fromParagraphs = paragraphs
                .Select(p => new CaseCandidate(Truncate(FirstLine(p), 250), p.Contains('\n') ? p : null, null))
                .ToList();
            return new ParseResult(fromParagraphs, new ParserDiagnostics("Paragraphs", fromParagraphs.Count, []));
        }

        var single = new List<CaseCandidate> { new(Truncate(fallbackTitle, 250), plainText, null) };
        return new ParseResult(single, new ParserDiagnostics("Single-paragraph fallback", 1, []));
    }

    private static List<CaseCandidate> ParseLinePattern(string plainText, Regex linePattern)
    {
        var result = new List<CaseCandidate>();
        foreach (var raw in plainText.Split('\n'))
        {
            var line = raw.TrimEnd('\r').TrimEnd();
            if (linePattern.IsMatch(line))
            {
                var cleaned = StripLinePrefix(line, linePattern);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    result.Add(new CaseCandidate(Truncate(cleaned, 250), null, null));
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Variant of <see cref="ParseLinePattern"/> that also captures the multi-line body of each
    /// item (everything between this line and the next line that matches the same prefix). Used
    /// for "ACn — Title" / "1. Title" style criteria where the criterion text is on subsequent
    /// lines (often starting with Gherkin keywords like "When..."). The first sentence of the
    /// body becomes the title when the prefix line had no inline title; otherwise the inline
    /// title wins and the entire body becomes the steps.
    /// </summary>
    /// <param name="plainText">Plain-text rendering of the AC HTML.</param>
    /// <param name="linePattern">Regex that matches the first line of each item.</param>
    /// <param name="keepPrefixInTitle">
    /// When true, the matched prefix line is kept verbatim (e.g. "AC1 — Quick Start Guide"). When
    /// false, the prefix (e.g. "AC1 —") is stripped and only the remainder is used as the title.
    /// AC-prefixed stories should keep the prefix so the generated TestRail case title matches
    /// the convention "ACn — Title" used by hand-authored cases.
    /// </param>
    private static List<CaseCandidate> ParseLinePatternWithBody(string plainText, Regex linePattern, bool keepPrefixInTitle = false)
    {
        var lines = plainText.Split('\n');
        var result = new List<CaseCandidate>();

        string? currentTitle = null;
        var bodyBuffer = new List<string>();

        void Flush()
        {
            if (currentTitle is null) return;
            var body = string.Join("\n", bodyBuffer.Where(b => !string.IsNullOrWhiteSpace(b))).Trim();
            var title = currentTitle;
            string? steps = null;

            if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(body))
            {
                // Prefix-only line (e.g. "AC1 —") with no inline title; promote first sentence of
                // the body to the title and keep the rest as steps.
                var firstLine = FirstLine(body);
                title = firstLine;
                steps = body.Length > firstLine.Length ? body[firstLine.Length..].TrimStart('\n', '\r', ' ') : null;
            }
            else if (!string.IsNullOrWhiteSpace(body))
            {
                steps = body;
            }

            // Split an inline "Expected Results:" / "Expected:" / "Then:" marker out of the
            // steps body so it lands in the dedicated Expected field. Story 410079's AC
            // format uses "<p>...shall...</p><p><u>Expected Results:</u> ...</p>" which the
            // plain-text rendering reduces to a body line starting with "Expected Results:".
            string? expected = null;
            if (!string.IsNullOrWhiteSpace(steps))
            {
                (steps, expected) = SplitExpectedResults(steps!);
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                result.Add(new CaseCandidate(Truncate(title.Trim(), 250),
                    string.IsNullOrWhiteSpace(steps) ? null : steps,
                    string.IsNullOrWhiteSpace(expected) ? null : expected));
            }
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r').TrimEnd();
            if (linePattern.IsMatch(line))
            {
                Flush();
                currentTitle = keepPrefixInTitle ? line.Trim() : StripLinePrefix(line, linePattern);
                bodyBuffer = new List<string>();
            }
            else if (currentTitle is not null)
            {
                bodyBuffer.Add(line);
            }
        }
        Flush();
        return result;
    }

    /// <summary>
    /// Splits a criterion body on the first <c>Expected Results:</c> / <c>Expected:</c> /
    /// <c>Then:</c> marker. Everything before the marker is returned as steps; everything
    /// after (with the marker itself stripped) is returned as the expected-results body.
    /// Returns <c>(body, null)</c> when no marker is found.
    /// </summary>
    internal static (string Steps, string? Expected) SplitExpectedResults(string body)
    {
        var match = ExpectedResultsMarkerRegex.Match(body);
        if (!match.Success) return (body.Trim(), null);

        var stepsPart = body[..match.Index].TrimEnd();
        var expectedPart = body[(match.Index + match.Length)..].Trim();

        if (string.IsNullOrWhiteSpace(expectedPart)) return (body.Trim(), null);
        return (stepsPart, expectedPart);
    }

    private static string StripLinePrefix(string line, Regex pattern)
    {
        // For all the "prefix" patterns, strip the matched prefix and keep the remainder as the title.
        var match = pattern.Match(line);
        if (!match.Success) return line.Trim();

        // Find the last run of non-whitespace delimiter/punctuation at the end of the match
        var end = match.Index + match.Length;
        if (end > 0 && end <= line.Length)
        {
            var remainder = line[(end - 1)..].TrimStart();
            // The patterns intentionally include the first char of the content so we don't chop words;
            // back off one char and re-trim leading delimiters.
            remainder = remainder.TrimStart('.', ')', ':', '-', '–', '—', ']', ' ', '\t');
            if (!string.IsNullOrWhiteSpace(remainder))
            {
                return remainder.Trim();
            }
        }
        return line.Trim();
    }

    private static List<CaseCandidate> ParseListItems(string html)
    {
        var listMatches = ListItemRegex.Matches(html);
        var result = new List<CaseCandidate>(listMatches.Count);
        foreach (Match m in listMatches)
        {
            var text = AzureDevOpsService.HtmlToPlainText(m.Groups["body"].Value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Add(new CaseCandidate(Truncate(text!, 250), null, null));
            }
        }
        return result;
    }

    private static List<CaseCandidate> ParseScenarioHeaders(string plainText)
    {
        var titles = new List<string>();
        foreach (var raw in plainText.Split('\n'))
        {
            var line = raw.Trim();
            var match = ScenarioHeaderRegex.Match(line);
            if (match.Success)
            {
                titles.Add(match.Groups["title"].Value.Trim());
            }
        }

        // Only treat as scenarios-only when none of the Gherkin body lines exist (guard against
        // the full-Gherkin branch above).
        if (titles.Count > 0)
        {
            return titles
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => new CaseCandidate(Truncate(t, 250), null, null))
                .ToList();
        }

        return [];
    }

    private static List<CaseCandidate> ParseTable(string html)
    {
        var rows = TableRowRegex.Matches(html);
        if (rows.Count < 2) return [];

        string?[]? headers = null;
        var result = new List<CaseCandidate>();

        foreach (Match row in rows)
        {
            var cells = TableCellRegex.Matches(row.Groups["body"].Value)
                .Select(c => AzureDevOpsService.HtmlToPlainText(c.Groups["body"].Value)?.Trim())
                .ToArray();

            if (cells.Length == 0) continue;

            if (headers is null)
            {
                headers = cells.Select(c => c?.ToLowerInvariant()).ToArray();
                continue;
            }

            // Map by header label when we can find Title / Steps / Expected columns; otherwise use first/second/third.
            int? titleIdx = null, stepsIdx = null, expectedIdx = null;
            for (var i = 0; i < headers.Length; i++)
            {
                var h = headers[i] ?? string.Empty;
                if (titleIdx is null && (h.Contains("ac") || h.Contains("criterion") || h.Contains("title") || h.Contains("scenario")))
                {
                    titleIdx = i;
                }
                else if (stepsIdx is null && (h.Contains("when") || h.Contains("step") || h.Contains("action")))
                {
                    stepsIdx = i;
                }
                else if (expectedIdx is null && (h.Contains("then") || h.Contains("expected") || h.Contains("result")))
                {
                    expectedIdx = i;
                }
            }

            // Fallback column mapping.
            titleIdx ??= 0;
            var title = SafeCell(cells, titleIdx.Value);
            if (string.IsNullOrWhiteSpace(title)) continue;

            var steps = stepsIdx is int si ? SafeCell(cells, si) : null;
            var expected = expectedIdx is int ei ? SafeCell(cells, ei) : null;

            result.Add(new CaseCandidate(Truncate(title, 250), NullIfEmpty(steps), NullIfEmpty(expected)));
        }

        // If we couldn't identify any title-ish header and only produced one column of generic rows,
        // treat this as "not really tabular AC" and fall through.
        if (result.Count < 2) return [];
        return result;
    }

    private static string? SafeCell(string?[] cells, int idx) => idx >= 0 && idx < cells.Length ? cells[idx] : null;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static List<CaseCandidate> ParseRuleExample(string plainText)
    {
        var result = new List<CaseCandidate>();
        string? currentRule = null;
        var exampleBuffer = new List<string>();

        void Flush()
        {
            if (currentRule is null && exampleBuffer.Count == 0) return;
            var title = currentRule ?? exampleBuffer[0];
            var steps = exampleBuffer.Count > 0 ? string.Join("\n", exampleBuffer) : null;
            result.Add(new CaseCandidate(Truncate(title, 250), steps, null));
            currentRule = null;
            exampleBuffer.Clear();
        }

        foreach (var raw in plainText.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var m = RuleOrExampleLineRegex.Match(line);
            if (!m.Success)
            {
                if (currentRule is not null) exampleBuffer.Add(line);
                continue;
            }

            var kw = m.Groups["kw"].Value.ToLowerInvariant();
            var body = m.Groups["body"].Value.Trim();
            if (kw == "rule")
            {
                Flush();
                currentRule = body;
            }
            else
            {
                // example
                exampleBuffer.Add("Example: " + body);
            }
        }
        Flush();
        return result;
    }

    private static List<CaseCandidate> ParseRequirementSentences(string plainText)
    {
        var result = new List<CaseCandidate>();
        var normalized = plainText.Replace('\n', ' ').Replace('\r', ' ');
        var sentences = SentenceSplitRegex.Split(normalized);
        foreach (var s in sentences)
        {
            var trimmed = s.Trim();
            if (trimmed.Length < 8) continue;
            if (ShallShouldMustRegex.IsMatch(trimmed))
            {
                result.Add(new CaseCandidate(Truncate(trimmed, 250), null, null));
            }
        }
        return result;
    }

    private static List<CaseCandidate> ParseGherkin(string plainText, string fallbackTitle)
    {
        var results = new List<CaseCandidate>();
        var lines = plainText.Split('\n');

        var givenBuffer = new List<string>();
        var whenBuffer = new List<string>();
        var thenBuffer = new List<string>();
        var scenarioTitle = fallbackTitle;
        var state = GherkinState.None;

        void Flush()
        {
            if (whenBuffer.Count == 0 && thenBuffer.Count == 0 && givenBuffer.Count == 0)
            {
                return;
            }

            var title = whenBuffer.Count > 0
                ? whenBuffer[0]
                : (thenBuffer.Count > 0 ? thenBuffer[0] : scenarioTitle);

            var steps = JoinBlocks(givenBuffer, whenBuffer);
            var expected = thenBuffer.Count > 0 ? string.Join("\n", thenBuffer) : null;
            results.Add(new CaseCandidate(Truncate(title, 250), steps, expected));

            givenBuffer.Clear();
            whenBuffer.Clear();
            thenBuffer.Clear();
            state = GherkinState.None;
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("Scenario", StringComparison.OrdinalIgnoreCase)
                && line.Contains(':'))
            {
                Flush();
                scenarioTitle = line[(line.IndexOf(':') + 1)..].Trim();
                continue;
            }

            var header = GherkinHeaderRegex.Match(line);
            if (!header.Success)
            {
                switch (state)
                {
                    case GherkinState.Given: givenBuffer.Add(line); break;
                    case GherkinState.When: whenBuffer.Add(line); break;
                    case GherkinState.Then: thenBuffer.Add(line); break;
                }

                continue;
            }

            var keyword = header.Groups["kw"].Value.ToLowerInvariant();
            switch (keyword)
            {
                case "given":
                    if (state == GherkinState.Then)
                    {
                        Flush();
                    }

                    state = GherkinState.Given;
                    givenBuffer.Add(line);
                    break;

                case "when":
                    state = GherkinState.When;
                    whenBuffer.Add(line);
                    break;

                case "then":
                    state = GherkinState.Then;
                    thenBuffer.Add(line);
                    break;

                case "and":
                case "but":
                    switch (state)
                    {
                        case GherkinState.Given: givenBuffer.Add(line); break;
                        case GherkinState.When: whenBuffer.Add(line); break;
                        case GherkinState.Then: thenBuffer.Add(line); break;
                    }

                    break;
            }
        }

        Flush();
        return results;
    }

    private static string? JoinBlocks(List<string> given, List<string> when)
    {
        if (given.Count == 0 && when.Count == 0)
        {
            return null;
        }

        var parts = new List<string>();
        if (given.Count > 0) parts.Add(string.Join("\n", given));
        if (when.Count > 0) parts.Add(string.Join("\n", when));
        return string.Join("\n\n", parts);
    }

    private static string FirstLine(string text)
    {
        var idx = text.IndexOf('\n');
        return idx < 0 ? text : text[..idx];
    }

    private static string Truncate(string value, int max)
    {
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }

    private enum GherkinState
    {
        None,
        Given,
        When,
        Then
    }

    /// <summary>
    /// Combined parse output: candidates plus a diagnostics record describing the strategy used.
    /// </summary>
    /// <param name="Candidates">The parsed candidate list (always at least one entry).</param>
    /// <param name="Diagnostics">Metadata about which strategy produced the list.</param>
    public record ParseResult(IReadOnlyList<CaseCandidate> Candidates, ParserDiagnostics Diagnostics);
}
