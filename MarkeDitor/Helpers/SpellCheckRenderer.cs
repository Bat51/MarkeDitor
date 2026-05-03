using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using MarkeDitor.Services;

namespace MarkeDitor.Helpers;

public record MisspelledWord(int Offset, int Length, string Text);

/// <summary>
/// Draws red wavy underlines under misspelled words. We only re-scan the
/// document on text changes (debounced upstream by the caller) and on
/// scroll, so this stays cheap on large docs.
/// </summary>
public class SpellCheckRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private readonly SpellCheckService _spell;
    private List<MisspelledWord> _misspelled = new();

    public SpellCheckRenderer(TextEditor editor, SpellCheckService spell)
    {
        _editor = editor;
        _spell = spell;
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public IReadOnlyList<MisspelledWord> Misspelled => _misspelled;

    public void Clear()
    {
        _misspelled = new List<MisspelledWord>();
        _editor.TextArea?.TextView?.InvalidateLayer(Layer);
    }

    /// <summary>Re-scan the entire document and refresh markers.</summary>
    public void Rescan()
    {
        var text = _editor.Document?.Text ?? string.Empty;
        var found = new List<MisspelledWord>();
        if (_spell.IsReady)
        {
            foreach (var m in WordTokenizer.EnumerateWords(text))
            {
                if (!_spell.Check(m.Text))
                    found.Add(m);
            }
        }
        _misspelled = found;
        _editor.TextArea?.TextView?.InvalidateLayer(Layer);
    }

    /// <summary>Find the misspelled word at a given document offset, if any.</summary>
    public MisspelledWord? AtOffset(int offset)
    {
        foreach (var w in _misspelled)
            if (offset >= w.Offset && offset <= w.Offset + w.Length)
                return w;
        return null;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid || _misspelled.Count == 0) return;

        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xff, 0x55, 0x55)), 1);
        var visualStart = textView.VisualLines[0].FirstDocumentLine.Offset;
        var visualEnd = textView.VisualLines[textView.VisualLines.Count - 1].LastDocumentLine.EndOffset;

        foreach (var word in _misspelled)
        {
            if (word.Offset > visualEnd) break;
            if (word.Offset + word.Length < visualStart) continue;

            var segment = new TextSegment { StartOffset = word.Offset, Length = word.Length };
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                DrawWavy(drawingContext, pen, rect.Left, rect.Bottom - 1, rect.Right);
            }
        }
    }

    private static void DrawWavy(DrawingContext ctx, Pen pen, double x0, double y, double x1)
    {
        // Tiny zig-zag: 2px tall, 4px period.
        const double period = 4;
        const double amp = 2;
        bool up = true;
        for (var x = x0; x < x1; x += period)
        {
            var nx = Math.Min(x + period, x1);
            var y1 = up ? y - amp : y;
            var y2 = up ? y : y - amp;
            ctx.DrawLine(pen, new Point(x, y1), new Point(nx, y2));
            up = !up;
        }
    }
}

/// <summary>Letters/digits + apostrophe inside word.</summary>
public static class WordTokenizer
{
    public static IEnumerable<MisspelledWord> EnumerateWords(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        var i = 0;
        while (i < text.Length)
        {
            if (!IsWordStart(text[i])) { i++; continue; }
            var start = i;
            while (i < text.Length && IsWordChar(text[i], i, text)) i++;
            var len = i - start;
            if (len >= 2)
                yield return new MisspelledWord(start, len, text.Substring(start, len));
        }
    }

    private static bool IsWordStart(char c) => char.IsLetter(c);

    private static bool IsWordChar(char c, int idx, string text)
    {
        if (char.IsLetter(c) || char.IsDigit(c)) return true;
        // Allow apostrophe inside word (e.g. "l'arbre").
        if ((c == '\'' || c == '’') && idx + 1 < text.Length && char.IsLetter(text[idx + 1]))
            return true;
        return false;
    }
}
