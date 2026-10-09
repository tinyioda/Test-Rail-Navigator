using AngleSharp.Html.Parser;
using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Unit coverage for ordinary Markdown rendering behavior.</summary>
public class MarkdownRendererTests
{
    /// <summary>Inline emphasis and hyperlinks render into the expected HTML elements.</summary>
    [Fact]
    public void ToHtml_RendersInlineFormattingAndLinks()
    {
        const string markdown = "**Bold** and *italic* with a [link](https://example.test/docs).";

        using var document = new HtmlParser().ParseDocument(MarkdownRenderer.ToHtml(markdown));

        Assert.Equal("Bold", document.QuerySelector("strong")?.TextContent);
        Assert.Equal("italic", document.QuerySelector("em")?.TextContent);
        Assert.Equal("https://example.test/docs", document.QuerySelector("a")?.GetAttribute("href"));
    }

    /// <summary>Lists, task lists, and fenced code blocks remain intact after rendering.</summary>
    [Fact]
    public void ToHtml_RendersListsTaskListsAndCodeBlocks()
    {
        const string markdown = """
            - first
            - second

            - [x] complete
            - [ ] pending

            ```csharp
            Console.WriteLine("hi");
            ```
            """;

        using var document = new HtmlParser().ParseDocument(MarkdownRenderer.ToHtml(markdown));

        Assert.Equal(4, document.QuerySelectorAll("li").Length);
        Assert.Equal(2, document.QuerySelectorAll("input[type='checkbox'][disabled]").Length);
        Assert.Contains("Console.WriteLine(\"hi\");", document.QuerySelector("pre code")?.TextContent);
    }

    /// <summary>Soft line breaks are rendered as explicit HTML line breaks.</summary>
    [Fact]
    public void ToHtml_RendersSoftLineBreaksAsBreakElements()
    {
        const string markdown = "Line one\nLine two";

        using var document = new HtmlParser().ParseDocument(MarkdownRenderer.ToHtml(markdown));

        Assert.NotNull(document.QuerySelector("p br"));
        Assert.Contains("Line one", document.QuerySelector("p")?.TextContent);
        Assert.Contains("Line two", document.QuerySelector("p")?.TextContent);
    }

    /// <summary>Null, empty, and whitespace-only inputs yield an empty fragment.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToHtml_ReturnsEmptyStringForBlankInput(string? markdown)
    {
        Assert.Equal(string.Empty, MarkdownRenderer.ToHtml(markdown));
    }
}
