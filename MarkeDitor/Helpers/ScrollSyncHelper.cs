using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using ColorTextBlock.Avalonia;
using Markdown.Avalonia;

namespace MarkeDitor.Helpers;

/// <summary>
/// Bidirectional scroll sync between AvaloniaEdit and Markdown.Avalonia.
///
/// Editor -> preview uses heading-based anchors: we collect the Y position
/// of every heading rendered in the preview tree and pair them in order
/// with the heading source lines parsed from the markdown. The editor's
/// top visible line is then mapped to a preview Y by piecewise linear
/// interpolation between adjacent anchors. This stays accurate even when
/// the preview contains heavy blocks (images, tables, code blocks) whose
/// height per source line varies dramatically — the pure ratio fallback
/// kicks in only when no headings are available.
///
/// Preview -> editor is the tricky direction. Markdown.Avalonia rebuilds
/// its content tree on every <c>Markdown</c> assignment and its inner
/// ScrollViewer emits a long tail of ScrollChanged events as the layout
/// settles. We therefore treat preview -> editor sync as opt-in: it only
/// fires when the user has just acted on the preview (mouse wheel).
/// </summary>
public class ScrollSyncHelper
{
    private static readonly Regex HeadingLineRegex =
        new(@"^#{1,6}\s+\S", RegexOptions.Compiled);
    private static readonly Regex FenceLineRegex =
        new(@"^[ ]{0,3}```", RegexOptions.Compiled);

    private readonly TextEditor _editor;
    private readonly MarkdownScrollViewer _preview;
    private ScrollViewer? _editorScroll;
    private ScrollViewer? _previewScroll;

    private DateTime _ignoreEditorUntil = DateTime.MinValue;
    private DateTime _ignorePreviewUntil = DateTime.MinValue;
    private DateTime _previewUserScrollUntil = DateTime.MinValue;
    private DateTime _resyncPreviewUntil = DateTime.MinValue;
    private bool _pendingEditorAlignment;

    // (markdown source line, preview content-Y) anchors, in source-line order.
    private readonly List<(int sourceLine, double contentY)> _anchors = new();
    private bool _anchorsDirty = true;
    private double _anchoredExtent;

    public ScrollSyncHelper(TextEditor editor, MarkdownScrollViewer preview)
    {
        _editor = editor;
        _preview = preview;

        editor.LayoutUpdated += (_, _) => TryHookEditor();
        preview.LayoutUpdated += OnPreviewLayoutUpdated;
        TryHookEditor();
        TryHookPreview();

        _preview.AddHandler(InputElement.PointerPressedEvent,
            OnPreviewPointerPressed, RoutingStrategies.Tunnel);
        _preview.AddHandler(InputElement.PointerWheelChangedEvent,
            OnPreviewWheel, RoutingStrategies.Tunnel);

        editor.TextChanged += OnEditorTextChanged;
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        _ignorePreviewUntil = now.AddMilliseconds(1500);
        _resyncPreviewUntil = now.AddMilliseconds(1500);
        _anchorsDirty = true;
    }

    private void OnPreviewLayoutUpdated(object? sender, EventArgs e)
    {
        TryHookPreview();
        // The preview's Extent grows as Markdown.Avalonia lays out images
        // and other heavy blocks; invalidate anchors when it moves so the
        // Y positions stay accurate.
        var ext = _previewScroll?.Extent.Height ?? 0;
        if (Math.Abs(ext - _anchoredExtent) > 0.5) _anchorsDirty = true;

        if (_pendingEditorAlignment || DateTime.UtcNow < _resyncPreviewUntil)
            ApplyEditorRatioToPreview();
    }

    private void OnPreviewWheel(object? sender, PointerWheelEventArgs e)
    {
        _previewUserScrollUntil = DateTime.UtcNow.AddMilliseconds(400);
        _pendingEditorAlignment = false;
    }

    private void TryHookEditor()
    {
        if (_editorScroll != null) return;
        _editorScroll = FindFirst<ScrollViewer>(_editor);
        if (_editorScroll != null)
            _editorScroll.ScrollChanged += OnEditorScrollChanged;
    }

    private void TryHookPreview()
    {
        if (_previewScroll != null) return;
        _previewScroll = FindFirst<ScrollViewer>(_preview);
        if (_previewScroll != null)
            _previewScroll.ScrollChanged += OnPreviewScrollChanged;
    }

    private static T? FindFirst<T>(Visual root) where T : Visual
    {
        foreach (var v in root.GetVisualDescendants())
            if (v is T t) return t;
        return null;
    }

    private void OnEditorScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DateTime.UtcNow < _ignoreEditorUntil) return;
        _pendingEditorAlignment = true;
        ApplyEditorRatioToPreview();
    }

    private void OnPreviewScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DateTime.UtcNow < _ignorePreviewUntil) return;
        if (DateTime.UtcNow >= _previewUserScrollUntil) return;
        if (_editorScroll == null || _previewScroll == null) return;

        // Preview->editor stays ratio-based: clicking through preview is a
        // rough navigation gesture and the user doesn't expect line-perfect
        // alignment back into the source.
        var maxOff = _previewScroll.Extent.Height - _previewScroll.Viewport.Height;
        if (maxOff <= 0) return;
        var ratio = _previewScroll.Offset.Y / maxOff;
        var editorMax = _editorScroll.Extent.Height - _editorScroll.Viewport.Height;
        if (editorMax <= 0) return;

        _ignoreEditorUntil = DateTime.UtcNow.AddMilliseconds(250);
        _editorScroll.Offset = new Vector(_editorScroll.Offset.X, ratio * editorMax);
    }

    private void ApplyEditorRatioToPreview()
    {
        if (_editorScroll == null || _previewScroll == null) return;
        var previewMax = _previewScroll.Extent.Height - _previewScroll.Viewport.Height;
        if (previewMax <= 0) return; // preview not laid out yet; LayoutUpdated will retry

        if (_anchorsDirty) RebuildAnchors();

        double? targetY = null;
        if (_anchors.Count > 0)
        {
            targetY = MapEditorLineToPreviewY(GetEditorTopLine());
        }
        if (!targetY.HasValue)
        {
            // No headings — fall back to whole-document ratio.
            var maxOff = _editorScroll.Extent.Height - _editorScroll.Viewport.Height;
            if (maxOff <= 0) return;
            var ratio = _editorScroll.Offset.Y / maxOff;
            targetY = ratio * previewMax;
        }

        var clamped = Math.Clamp(targetY.Value, 0, previewMax);
        var bumped = DateTime.UtcNow.AddMilliseconds(300);
        if (bumped > _ignorePreviewUntil) _ignorePreviewUntil = bumped;
        _previewScroll.Offset = new Vector(_previewScroll.Offset.X, clamped);
        _pendingEditorAlignment = false;
    }

    private void RebuildAnchors()
    {
        _anchors.Clear();
        _anchoredExtent = _previewScroll?.Extent.Height ?? 0;
        _anchorsDirty = false;

        var doc = _editor.Document;
        if (doc == null || _previewScroll == null) return;

        // 1) Collect heading source lines from the markdown, skipping over
        // anything inside a fenced code block — `# foo` inside a Python
        // sample is not a heading and Markdown.Avalonia won't render it
        // as one either.
        var headingLines = new List<int>();
        var inFence = false;
        for (int i = 1; i <= doc.LineCount; i++)
        {
            var line = doc.GetLineByNumber(i);
            var text = doc.GetText(line);
            if (FenceLineRegex.IsMatch(text))
            {
                inFence = !inFence;
                continue;
            }
            if (inFence) continue;
            if (HeadingLineRegex.IsMatch(text))
                headingLines.Add(i);
        }
        if (headingLines.Count == 0) return;

        // 2) Walk the preview tree for heading-class CTextBlocks in document
        // order and capture each one's Y in the scroller's content space.
        var previewYs = new List<double>();
        foreach (var v in _preview.GetVisualDescendants())
        {
            if (v is CTextBlock tb && IsHeadingClass(tb))
            {
                var p = tb.TranslatePoint(new Point(0, 0), _previewScroll);
                if (p.HasValue)
                    previewYs.Add(p.Value.Y + _previewScroll.Offset.Y);
            }
        }
        if (previewYs.Count == 0) return;

        // 3) Pair them positionally. If the counts disagree (unlikely but
        // possible if a plugin emits extra blocks) we just use the shorter
        // list; the fallback at the tail covers the rest.
        var count = Math.Min(headingLines.Count, previewYs.Count);
        for (int i = 0; i < count; i++)
            _anchors.Add((headingLines[i], previewYs[i]));
    }

    private static bool IsHeadingClass(CTextBlock tb)
    {
        foreach (var c in tb.Classes)
            if (c != null && c.StartsWith("Heading", StringComparison.Ordinal))
                return true;
        return false;
    }

    private double? MapEditorLineToPreviewY(int line)
    {
        if (_anchors.Count == 0 || _previewScroll == null) return null;

        // Before the first heading: linearly approach the first anchor.
        if (line <= _anchors[0].sourceLine)
        {
            var span = Math.Max(1, _anchors[0].sourceLine - 1);
            var t = (double)(line - 1) / span;
            return Math.Max(0, t) * _anchors[0].contentY;
        }

        for (int i = 0; i < _anchors.Count - 1; i++)
        {
            var a = _anchors[i];
            var b = _anchors[i + 1];
            if (line >= a.sourceLine && line < b.sourceLine)
            {
                var t = (double)(line - a.sourceLine) / (b.sourceLine - a.sourceLine);
                return a.contentY + t * (b.contentY - a.contentY);
            }
        }

        // After the last heading: extrapolate using the average line-height
        // of the previous section so the tail of the document doesn't snap
        // to the heading position.
        var last = _anchors[^1];
        var lineCount = _editor.Document?.LineCount ?? last.sourceLine;
        var totalH = _previewScroll.Extent.Height;
        var remainingLines = Math.Max(1, lineCount - last.sourceLine);
        var remainingH = Math.Max(0, totalH - last.contentY);
        return last.contentY + (double)(line - last.sourceLine) / remainingLines * remainingH;
    }

    private int GetEditorTopLine()
    {
        var firstVisualLine = _editor.TextArea.TextView.VisualLines.FirstOrDefault();
        return firstVisualLine?.FirstDocumentLine.LineNumber ?? 1;
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        TryHookPreview();
        if (_previewScroll == null) return;
        var properties = e.GetCurrentPoint(_preview).Properties;
        if (!properties.IsLeftButtonPressed) return;

        var pos = e.GetPosition(_previewScroll);
        var totalH = _previewScroll.Extent.Height;
        if (totalH <= 0) return;

        var docY = _previewScroll.Offset.Y + pos.Y;
        var ratio = Math.Clamp(docY / totalH, 0.0, 1.0);

        var lineCount = _editor.Document.LineCount;
        var targetLine = (int)Math.Round(ratio * (lineCount - 1)) + 1;
        targetLine = Math.Clamp(targetLine, 1, lineCount);

        _ignoreEditorUntil = DateTime.UtcNow.AddMilliseconds(250);
        _editor.ScrollToLine(targetLine);
        var line = _editor.Document.GetLineByNumber(targetLine);
        _editor.CaretOffset = line.Offset;
    }
}
