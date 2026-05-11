using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkeDitor.Helpers;

/// <summary>
/// Transforms a markdown document before it is handed to Markdown.Avalonia
/// for live preview. Each transformation also returns a map of in-document
/// anchor ids to the user-visible heading text we expect to find in the
/// rendered preview tree, so a custom HyperlinkCommand can scroll to it
/// when an in-document link is clicked.
/// </summary>
public sealed class PreprocessedMarkdown
{
    public string Markdown { get; init; } = string.Empty;

    /// <summary>Maps a heading <c>{#id}</c> annotation to the heading's
    /// rendered text. <c>null</c> entries mean "scroll to the Footnotes
    /// section" (used for footnote refs that share a single landing
    /// spot).</summary>
    public Dictionary<string, string> AnchorTargets { get; init; } = new();
}

public static class MarkdownPreprocessor
{
    private static readonly Regex HeadingIdRegex = new(
        @"^(#{1,6}[ \t]+)(.+?)[ \t]+\{#([A-Za-z][\w-]*)\}[ \t]*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex FootnoteDefRegex = new(
        @"^[ ]{0,3}\[\^([^\]\s]+)\]:[ \t]+(.+?)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex FootnoteRefRegex = new(
        @"\[\^([^\]\s]+)\]",
        RegexOptions.Compiled);

    // Definition list: a non-marker line immediately followed by a line
    // starting with ": " (or its tab equivalent). We exclude the usual
    // markdown block markers from the term line so we don't capture
    // headings, lists, blockquotes or fenced blocks by accident.
    private static readonly Regex DefListRegex = new(
        @"^(?![ \t]*(?:#|>|\*|-|\+|\d+[.)]|```|~~~|\[\^))([^\r\n][^\r\n]*?)\r?\n[ ]{0,3}:[ \t]+(.+?)(?:\r?\n|$)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public const string FootnotesAnchor = "__markeditor_footnotes__";
    public const string FootnotesHeadingText = "Footnotes";

    public static PreprocessedMarkdown Process(string? source)
    {
        var input = source ?? string.Empty;
        var anchors = new Dictionary<string, string>(StringComparer.Ordinal);

        // 1) Strip heading-id annotations and remember each id -> heading text
        // so [link](#custom-id) can later resolve via the visual tree.
        var afterHeadings = HeadingIdRegex.Replace(input, m =>
        {
            var prefix = m.Groups[1].Value;
            var title = m.Groups[2].Value.TrimEnd();
            var id = m.Groups[3].Value;
            anchors[id] = title;
            return prefix + title;
        });

        // 2a) Definition lists: `term\n: def` -> bold term followed by an
        // indented italic blockquote-style definition so each pair reads
        // as a small "card" in the preview.
        afterHeadings = DefListRegex.Replace(afterHeadings, m =>
        {
            var term = m.Groups[1].Value.Trim();
            var def = m.Groups[2].Value.Trim();
            return $"**{term}**\n\n> *{def}*";
        });

        // 2) Extract footnote definitions and remember them in document order.
        var definitions = new List<(string id, string text)>();
        var seenDefIds = new HashSet<string>(StringComparer.Ordinal);
        var withoutDefs = FootnoteDefRegex.Replace(afterHeadings, m =>
        {
            var id = m.Groups[1].Value;
            var text = m.Groups[2].Value.Trim();
            if (seenDefIds.Add(id)) definitions.Add((id, text));
            return string.Empty;
        });

        if (definitions.Count == 0)
            return new PreprocessedMarkdown { Markdown = withoutDefs, AnchorTargets = anchors };

        // 3) Number footnotes by the order their first reference appears so
        // the rendered numbers read naturally. Definitions never referenced
        // get appended after the rest.
        var idToNumber = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match m in FootnoteRefRegex.Matches(withoutDefs))
        {
            var id = m.Groups[1].Value;
            if (seenDefIds.Contains(id) && !idToNumber.ContainsKey(id))
                idToNumber[id] = idToNumber.Count + 1;
        }
        foreach (var (id, _) in definitions)
        {
            if (!idToNumber.ContainsKey(id))
                idToNumber[id] = idToNumber.Count + 1;
        }

        // 4) Rewrite inline footnote refs as clickable links. Each ref points
        // at its own anchor so the HyperlinkCommand can land on the exact
        // definition; we also expose the Footnotes section heading as a
        // common fallback if the per-footnote text match fails.
        var defTextByNumber = new Dictionary<int, string>();
        foreach (var (id, text) in definitions)
            defTextByNumber[idToNumber[id]] = text;

        var rewritten = FootnoteRefRegex.Replace(withoutDefs, m =>
        {
            var id = m.Groups[1].Value;
            if (!idToNumber.TryGetValue(id, out var n)) return m.Value;
            if (defTextByNumber.TryGetValue(n, out var defText))
                anchors[$"fn-{n}"] = defText;
            return $"[{n}](#fn-{n})";
        });

        // 5) Append the Footnotes section. We keep it as an ordered list so
        // Markdown.Avalonia renders it consistently with other lists in the
        // document; the leading heading "Footnotes" is what we scroll to.
        // The top padding of the section is set via an Avalonia style on
        // Heading2 in MainWindow.axaml (Markdown.Avalonia does not parse
        // raw HTML <br/> and `&nbsp;` paragraphs in inline-paragraph mode).
        var sb = new StringBuilder(rewritten.TrimEnd());
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## " + FootnotesHeadingText);
        sb.AppendLine();
        // Emit in numbered order
        var orderedByNumber = new (int n, string text)[definitions.Count];
        foreach (var (id, text) in definitions)
            orderedByNumber[idToNumber[id] - 1] = (idToNumber[id], text);
        foreach (var entry in orderedByNumber)
            sb.AppendLine(entry.n.ToString() + ". " + entry.text);

        anchors[FootnotesAnchor] = FootnotesHeadingText;

        return new PreprocessedMarkdown
        {
            Markdown = sb.ToString(),
            AnchorTargets = anchors,
        };
    }
}
