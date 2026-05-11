using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ColorTextBlock.Avalonia;
using Markdown.Avalonia;

namespace MarkeDitor.Helpers;

/// <summary>
/// Replacement for Markdown.Avalonia's default hyperlink command. URLs that
/// start with "#" are treated as in-document anchors and trigger a scroll
/// to a matching heading in the preview tree. Everything else is handed
/// off to the OS shell to open in the user's browser / mail client.
/// </summary>
public class PreviewLinkCommand : ICommand
{
    private readonly MarkdownScrollViewer _preview;

    public Dictionary<string, string> AnchorTargets { get; set; } = new();

    public PreviewLinkCommand(MarkdownScrollViewer preview)
    {
        _preview = preview;
    }

    public bool CanExecute(object? parameter) => true;
#pragma warning disable CS0067 // ICommand contract requires the event; we never raise it.
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public void Execute(object? parameter)
    {
        if (parameter is not string url || string.IsNullOrEmpty(url)) return;

        if (url.StartsWith("#", StringComparison.Ordinal))
        {
            var id = url.Substring(1);
            if (AnchorTargets.TryGetValue(id, out var targetText))
                ScrollToTextBlockByText(targetText);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // The user clicked a link the OS couldn't open. Nothing useful
            // we can do beyond not crashing.
        }
    }

    private void ScrollToTextBlockByText(string targetText)
    {
        var scrollViewer = FindFirst<ScrollViewer>(_preview);
        if (scrollViewer == null) return;

        // Prefer an exact heading match (most stable), then fall back to any
        // CTextBlock with the same text. The fallback covers per-footnote
        // anchors which target ordered-list items rather than headings.
        CTextBlock? headingMatch = null;
        CTextBlock? anyMatch = null;
        foreach (var v in _preview.GetVisualDescendants())
        {
            if (v is not CTextBlock tb) continue;
            if (!string.Equals(tb.Text, targetText, StringComparison.Ordinal)) continue;
            anyMatch ??= tb;
            if (tb.Classes.Any(c => c != null && c.StartsWith("Heading", StringComparison.Ordinal)))
            {
                headingMatch = tb;
                break;
            }
        }
        var match = headingMatch ?? anyMatch;
        if (match == null) return;

        var point = match.TranslatePoint(new Point(0, 0), scrollViewer);
        if (!point.HasValue) return;
        var targetY = point.Value.Y + scrollViewer.Offset.Y;
        var max = scrollViewer.Extent.Height - scrollViewer.Viewport.Height;
        if (max <= 0) return;
        var clamped = Math.Clamp(targetY, 0, max);
        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, clamped);
    }

    private static T? FindFirst<T>(Visual root) where T : Visual
    {
        foreach (var v in root.GetVisualDescendants())
            if (v is T t) return t;
        return null;
    }
}
