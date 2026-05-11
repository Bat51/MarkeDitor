using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ColorTextBlock.Avalonia;
using Markdown.Avalonia.Parsers;
using Markdown.Avalonia.Plugins;

namespace MarkeDitor.Helpers;

/// <summary>
/// Inline plugin that swallows GitHub-style heading ID annotations such as
/// <c># Heading {#my-id}</c>. Markdig (used for HTML export) honours them
/// natively; here we just hide the syntax from the live preview.
/// </summary>
public class HeadingIdPlugin : IMdAvPlugin
{
    private static readonly Regex Pattern = new(
        @"\s*\{#[A-Za-z][\w-]*\}",
        RegexOptions.Compiled);

    public void Setup(SetupInfo info)
    {
        info.Register(InlineParser.New(Pattern, "MarkeDitor.HeadingId",
            _ => new CRun { Text = string.Empty }));
    }
}

/// <summary>
/// Inline plugin that turns the leading <c>[ ]</c> / <c>[x]</c> of a task
/// list item into a Unicode checkbox. The regex is anchored at the start
/// of the inline text segment, which corresponds to the first character
/// after the list marker, so it does not match brackets in body prose.
/// </summary>
public class TaskListPlugin : IMdAvPlugin
{
    private static readonly Regex Pattern = new(
        @"^\[([ xX])\]\s",
        RegexOptions.Compiled);

    public void Setup(SetupInfo info)
    {
        info.Register(InlineParser.New(Pattern, "MarkeDitor.TaskList", m =>
        {
            var done = m.Groups[1].Value.Trim().Length > 0;
            // Coloured + bold so the checkbox reads as the dominant marker
            // rather than competing visually with the list bullet that
            // Markdown.Avalonia draws to its left.
            return new CRun
            {
                Text = done ? "☑ " : "☐ ",
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(done
                    ? Color.FromRgb(0x6c, 0xc0, 0x6c)
                    : Color.FromRgb(0x58, 0xa6, 0xff)),
            };
        }));
    }
}

/// <summary>
/// Inline plugin for the <c>==highlight==</c> extended-markdown syntax.
/// Renders the matched text on a yellow background; the colour bound to
/// AppHighlightBrush via theme resources so light/dark themes both work.
/// </summary>
public class HighlightPlugin : IMdAvPlugin
{
    private static readonly Regex Pattern = new(
        @"==(?=\S)(.+?)(?<=\S)==",
        RegexOptions.Compiled);

    public void Setup(SetupInfo info)
    {
        info.Register(InlineParser.New(Pattern, "MarkeDitor.Highlight", m =>
        {
            var run = new CRun
            {
                Text = m.Groups[1].Value,
                Background = new SolidColorBrush(Color.FromRgb(0xff, 0xf2, 0x9d)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x1f, 0x23, 0x28)),
            };
            return run;
        }));
    }
}

/// <summary>
/// Inline plugin for the <c>~text~</c> subscript syntax. CRun has a
/// TextVerticalAlignment property which we set to Bottom, combined with a
/// reduced font size, to approximate a true subscript.
/// </summary>
public class SubscriptPlugin : IMdAvPlugin
{
    private static readonly Regex Pattern = new(
        @"(?<![~\\])~(?!~)([^~\s][^~]*?)~(?!~)",
        RegexOptions.Compiled);

    public void Setup(SetupInfo info)
    {
        info.Register(InlineParser.New(Pattern, "MarkeDitor.Subscript", m => new CRun
        {
            Text = m.Groups[1].Value,
            FontSize = 10,
            TextVerticalAlignment = TextVerticalAlignment.Bottom,
        }));
    }
}

/// <summary>
/// Inline plugin for the <c>^text^</c> superscript syntax. Mirror of
/// <see cref="SubscriptPlugin"/> using TextVerticalAlignment=Top.
/// </summary>
public class SuperscriptPlugin : IMdAvPlugin
{
    private static readonly Regex Pattern = new(
        @"\^([^\s\^][^\^]*?)\^",
        RegexOptions.Compiled);

    public void Setup(SetupInfo info)
    {
        info.Register(InlineParser.New(Pattern, "MarkeDitor.Superscript", m => new CRun
        {
            Text = m.Groups[1].Value,
            FontSize = 10,
            TextVerticalAlignment = TextVerticalAlignment.Top,
        }));
    }
}

/// <summary>
/// Inline plugin for footnote references like <c>[^name]</c>. Renders as a
/// small bracketed token in muted colour; the actual numbering and the
/// "Footnotes" section are produced by <see cref="FootnoteDefPlugin"/> on
/// the block side. Both shapes get full fidelity in the HTML export.
/// </summary>
public class FootnoteRefPlugin : IMdAvPlugin
{
    private static readonly Regex Pattern = new(
        @"\[\^([^\]]+)\]",
        RegexOptions.Compiled);

    public void Setup(SetupInfo info)
    {
        info.Register(InlineParser.New(Pattern, "MarkeDitor.FootnoteRef", m => new CRun
        {
            Text = "[" + m.Groups[1].Value + "]",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x58, 0xa6, 0xff)),
        }));
    }
}

/// <summary>
/// Block plugin for footnote definitions like <c>[^name]: explanation</c>.
/// Renders the definition as a small muted paragraph prefixed with the id;
/// keeps it in flow rather than collecting at the end of the document, so
/// no global state is needed and the preview stays a pure transformation.
/// </summary>
public class FootnoteDefPlugin : IMdAvPlugin
{
    private static readonly Regex Pattern = new(
        @"^[ ]{0,3}\[\^([^\]]+)\]:[ \t]+(.+?)(?:\r?\n|$)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public void Setup(SetupInfo info)
    {
        var parser = BlockParser.New(Pattern, "MarkeDitor.FootnoteDef",
            (Func<Match, IEnumerable<Control>>)(m => Render(m)));
        info.RegisterTop(parser);
    }

    private static IEnumerable<Control> Render(Match m)
    {
        var id = m.Groups[1].Value;
        var text = m.Groups[2].Value;
        var tb = new TextBlock
        {
            Text = $"[{id}] {text}",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x76, 0x83, 0x90)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2),
        };
        yield return tb;
    }
}
