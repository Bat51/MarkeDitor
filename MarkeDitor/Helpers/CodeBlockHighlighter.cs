using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Markdown.Avalonia.Parsers;
using Markdown.Avalonia.Plugins;
using TextMateSharp.Grammars;

namespace MarkeDitor.Helpers;

/// <summary>
/// Markdown.Avalonia plugin that replaces the built-in fenced code block
/// renderer with a read-only AvaloniaEdit TextEditor wired to TextMate so
/// the rendered code block in the preview is syntax-highlighted with the
/// same theme as the editor on the left.
/// </summary>
public class TextMateCodeBlockPlugin : IMdAvPlugin
{
    private static readonly Regex FencedCodeBlockRegex = new(
        @"^[ ]{0,3}```[ \t]*(?<lang>[A-Za-z0-9_+\-#]*)[ \t]*\r?\n(?<code>[\s\S]*?)\r?\n[ ]{0,3}```[ \t]*(?:\r?\n|$)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public void Setup(SetupInfo info)
    {
        var parser = BlockParser.New(
            FencedCodeBlockRegex,
            "MarkeDitor.FencedCodeBlock",
            (Func<Match, IEnumerable<Control>>)(m => Render(m)));
        info.RegisterTop(parser);
    }

    private static IEnumerable<Control> Render(Match m)
    {
        var lang = m.Groups["lang"].Value.Trim().ToLowerInvariant();
        var code = m.Groups["code"].Value;

        var editor = new TextEditor
        {
            Text = code,
            IsReadOnly = true,
            ShowLineNumbers = false,
            FontFamily = new FontFamily("Cascadia Code,Consolas,Menlo,Monospace"),
            FontSize = 13,
            Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x22, 0x29)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xcd, 0xd9, 0xe5)),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Padding = new Thickness(8),
        };

        // Hook TextMate AFTER the editor is loaded into the visual tree,
        // otherwise InstallTextMate fails because the TextArea is null.
        editor.AttachedToVisualTree += (_, _) =>
        {
            try
            {
                var registry = new RegistryOptions(ThemeName.DarkPlus);
                var tm = editor.InstallTextMate(registry);
                var langId = MapLanguage(lang);
                if (!string.IsNullOrEmpty(langId))
                {
                    var scope = registry.GetScopeByLanguageId(langId);
                    if (!string.IsNullOrEmpty(scope))
                        tm.SetGrammar(scope);
                }
            }
            catch { /* fall back to plain text */ }
        };

        var border = new Border
        {
            Margin = new Thickness(0, 6),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x37, 0x3e, 0x47)),
            Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x22, 0x29)),
            Child = editor,
        };
        yield return border;
    }

    /// <summary>
    /// Map Markdown's loose language identifiers to TextMate's language IDs.
    /// Anything not mapped returns the input unchanged so unknown languages
    /// still get whatever grammar TextMate happens to recognise.
    /// </summary>
    private static string MapLanguage(string lang) => lang switch
    {
        "" or "text" or "plaintext" or "txt" => "",
        "js"  or "node" or "nodejs" => "javascript",
        "ts"                        => "typescript",
        "py"  or "python3"          => "python",
        "rb"                        => "ruby",
        "sh"  or "bash" or "shell"  => "shellscript",
        "yml"                       => "yaml",
        "md"                        => "markdown",
        "cs"                        => "csharp",
        "c++" or "cpp"              => "cpp",
        "h" or "hpp"                => "cpp",
        "rs"                        => "rust",
        "kt"                        => "kotlin",
        _ => lang,
    };
}
