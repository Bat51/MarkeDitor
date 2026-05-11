using System.IO;
using System.Net;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace MarkeDitor.Services;

public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public string ToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        var document = Markdig.Markdown.Parse(markdown, _pipeline);

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        _pipeline.Setup(renderer);

        // Inject data-line on every top-level block at the exact moment the
        // renderer is about to emit it, so the attributes always land in HTML.
        renderer.ObjectWriteBefore += (r, obj) =>
        {
            if (obj is Block block && block.Parent is MarkdownDocument && block.Line >= 0)
            {
                block.GetAttributes().AddProperty("data-line", (block.Line + 1).ToString());
            }
        };

        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    /// <summary>
    /// Wrap the rendered HTML body in a standalone document with the default
    /// stylesheet so users can open the exported file directly in a browser.
    /// </summary>
    public string ToHtmlDocument(string markdown, string title)
    {
        var body = ToHtml(markdown);
        var safeTitle = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(title) ? "Document" : title);
        return $"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{safeTitle}</title>
<style>
{DefaultCss}
</style>
</head>
<body>
<article class="markdown-body">
{body}
</article>
</body>
</html>
""";
    }

    /// <summary>
    /// Lightweight GitHub-flavoured stylesheet shipped with HTML exports.
    /// Kept here as a single string so a future "edit CSS" preference can
    /// expose / override it without touching the export pipeline.
    /// </summary>
    public const string DefaultCss = """
body { font-family: -apple-system, "Segoe UI", Helvetica, Arial, sans-serif; line-height: 1.6; color: #1f2328; background: #ffffff; max-width: 860px; margin: 2em auto; padding: 0 1em; }
article.markdown-body { font-size: 16px; }
h1, h2, h3, h4, h5, h6 { font-weight: 600; line-height: 1.25; margin-top: 1.6em; margin-bottom: 0.6em; }
h1 { font-size: 2em; border-bottom: 1px solid #d0d7de; padding-bottom: 0.3em; }
h2 { font-size: 1.5em; border-bottom: 1px solid #d0d7de; padding-bottom: 0.3em; }
h3 { font-size: 1.25em; }
h4 { font-size: 1em; }
h5 { font-size: 0.875em; }
h6 { font-size: 0.85em; color: #656d76; }
p { margin: 0 0 1em; }
a { color: #0969da; text-decoration: none; }
a:hover { text-decoration: underline; }
ul, ol { margin: 0 0 1em; padding-left: 2em; }
li > p { margin-top: 1em; }
blockquote { margin: 0 0 1em; padding: 0 1em; color: #656d76; border-left: 0.25em solid #d0d7de; }
hr { height: 1px; background: #d0d7de; border: 0; margin: 1.5em 0; }
img { max-width: 100%; }
code { font-family: ui-monospace, "Cascadia Code", "Consolas", "Menlo", monospace; background: #f6f8fa; padding: 0.15em 0.35em; border-radius: 3px; font-size: 0.9em; border: 1px solid #d0d7de; }
pre { background: #f6f8fa; border: 1px solid #d0d7de; border-radius: 6px; padding: 1em; overflow-x: auto; }
pre > code { background: transparent; border: 0; padding: 0; font-size: 0.875em; }
table { border-collapse: collapse; margin: 0 0 1em; display: block; overflow-x: auto; }
th, td { border: 1px solid #d0d7de; padding: 0.4em 0.75em; }
th { background: #f6f8fa; font-weight: 600; }
tr:nth-child(2n) td { background: #f6f8fa; }
""";
}
