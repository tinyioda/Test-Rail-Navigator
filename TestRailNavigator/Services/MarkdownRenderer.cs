using Markdig;

namespace TestRailNavigator.Services;

/// <summary>
/// Renders TestRail free-form text (comments, descriptions) — which is stored in
/// Markdown — to safe HTML for display. Uses the same advanced extensions TestRail
/// supports (GFM tables, fenced code, autolinks, strikethrough, task lists).
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .DisableHtml() // strip raw HTML to avoid XSS from user-entered comments
        .Build();

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

        return Markdown.ToHtml(markdown, Pipeline);
    }
}
