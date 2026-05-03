using System.IO;
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
}
