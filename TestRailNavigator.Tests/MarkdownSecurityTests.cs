using AngleSharp.Html.Parser;
using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Guards rendered TestRail comments against stored XSS without losing ordinary Markdown.</summary>
public class MarkdownSecurityTests
{
    /// <summary>Raw markup, generic attributes, and executable URL schemes cannot reach the DOM.</summary>
    [Theory]
    [InlineData("![x](/missing.png){onerror=\"alert(1)\"}")]
    [InlineData("[link](https://safe.invalid){onclick=\"alert(1)\"}")]
    [InlineData("[link](javascript:alert%281%29)")]
    [InlineData("[link](JaVaScRiPt:alert%281%29)")]
    [InlineData("[link](java&#x73;cript:alert%281%29)")]
    [InlineData("![x](data:image/svg+xml;base64,PHN2ZyBvbmxvYWQ9YWxlcnQoMSk+)")]
    [InlineData("<img src=x onerror=\"alert(1)\"><script>alert(1)</script>")]
    [InlineData("<svg onload=\"alert(1)\"></svg><iframe srcdoc=\"<script>alert(1)</script>\"></iframe>")]
    public void DangerousMarkdownDoesNotProduceExecutableHtml(string markdown)
    {
        using var document = new HtmlParser().ParseDocument(MarkdownRenderer.ToHtml(markdown));
        Assert.Empty(document.QuerySelectorAll("script, iframe, object, embed, svg, math, style, form"));
        foreach (var element in document.All)
        {
            foreach (var attribute in element.Attributes)
            {
                Assert.False(attribute.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(attribute.Name, new[] { "style", "srcdoc", "formaction", "id", "name" });
                if (attribute.Name is "href" or "src")
                {
                    Assert.DoesNotMatch(@"(?i)^\s*(?:javascript|vbscript|data)\s*:", attribute.Value);
                }
            }
        }
    }

    /// <summary>Headings, links, images, tables, code, strikethrough, and task lists remain usable.</summary>
    [Fact]
    public void OrdinaryMarkdownFormattingIsPreserved()
    {
        const string markdown = """
            # Result
            **Passed** with *notes* and ~~old text~~.

            [Documentation](https://safe.invalid/docs)
            ![Evidence](/evidence.png)

            | Name | Status |
            | --- | --- |
            | Login | Passed |

            - [x] Completed
            - [ ] Pending

            ```html
            <script>text only</script>
            ```
            """;
        using var document = new HtmlParser().ParseDocument(MarkdownRenderer.ToHtml(markdown));
        Assert.Equal("Result", document.QuerySelector("h1")?.TextContent);
        Assert.Equal("Passed", document.QuerySelector("strong")?.TextContent);
        Assert.NotNull(document.QuerySelector("em"));
        Assert.NotNull(document.QuerySelector("del"));
        Assert.Equal("https://safe.invalid/docs", document.QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("/evidence.png", document.QuerySelector("img")?.GetAttribute("src"));
        Assert.Equal(2, document.QuerySelectorAll("table th").Length);
        Assert.Equal(2, document.QuerySelectorAll("input[type='checkbox'][disabled]").Length);
        Assert.Contains("<script>text only</script>", document.QuerySelector("pre code")?.TextContent);
        Assert.Empty(document.QuerySelectorAll("script"));
    }

    /// <summary>Empty values do not produce markup or errors.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyMarkdownIsEmpty(string? markdown) => Assert.Equal(string.Empty, MarkdownRenderer.ToHtml(markdown));
}
