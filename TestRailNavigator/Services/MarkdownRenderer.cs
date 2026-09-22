using Ganss.Xss;
using Markdig;
using Markdig.Extensions.EmphasisExtras;

namespace TestRailNavigator.Services;

/// <summary>
/// Renders TestRail free-form text (comments, descriptions) — which is stored in
/// Markdown — to sanitized HTML using explicitly enabled formatting extensions.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAutoLinks()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseTaskLists()
        .UseSoftlineBreakAsHardlineBreak()
        .DisableHtml()
        .Build();

    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    /// <summary>
    /// Converts a Markdown string to an HTML fragment. Returns an empty string
    /// when the input is null or whitespace.
    /// </summary>
    /// <param name="markdown">The Markdown source text.</param>
    /// <returns>The rendered HTML.</returns>
    public static string ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        return Sanitizer.Sanitize(Markdown.ToHtml(markdown, Pipeline));
    }

    /// <summary>Restricts rendered content to Markdown formatting and non-executable links.</summary>
    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(
        [
            "a", "p", "br", "em", "strong", "del", "code", "pre", "blockquote",
            "ol", "ul", "li", "hr", "h1", "h2", "h3", "h4", "h5", "h6",
            "table", "thead", "tbody", "tr", "th", "td", "img", "input"
        ]);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(
        [
            "href", "title", "src", "alt", "start", "colspan", "rowspan",
            "type", "checked", "disabled"
        ]);
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["https", "http", "mailto"]);
        return sanitizer;
    }
}
