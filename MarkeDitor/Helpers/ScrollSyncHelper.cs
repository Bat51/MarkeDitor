using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Markdown.Avalonia;

namespace MarkeDitor.Helpers;

/// <summary>
/// Bidirectional scroll sync between AvaloniaEdit and Markdown.Avalonia.
/// Both controls expose an inner ScrollViewer in their template; we hook
/// ScrollChanged on each, with mutual ignore-windows to avoid feedback.
/// </summary>
public class ScrollSyncHelper
{
    private readonly TextEditor _editor;
    private readonly MarkdownScrollViewer _preview;
    private ScrollViewer? _editorScroll;
    private ScrollViewer? _previewScroll;

    private DateTime _ignoreEditorUntil = DateTime.MinValue;
    private DateTime _ignorePreviewUntil = DateTime.MinValue;

    public ScrollSyncHelper(TextEditor editor, MarkdownScrollViewer preview)
    {
        _editor = editor;
        _preview = preview;

        // Templates may not be applied at construction time; retry on each
        // layout pass until both inner ScrollViewers are found.
        editor.LayoutUpdated += (_, _) => TryHookEditor();
        preview.LayoutUpdated += (_, _) => TryHookPreview();
        TryHookEditor();
        TryHookPreview();

        _preview.AddHandler(InputElement.PointerPressedEvent,
            OnPreviewPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
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
        if (_editorScroll == null || _previewScroll == null) return;

        var maxOff = _editorScroll.Extent.Height - _editorScroll.Viewport.Height;
        if (maxOff <= 0) return;
        var ratio = _editorScroll.Offset.Y / maxOff;
        var previewMax = _previewScroll.Extent.Height - _previewScroll.Viewport.Height;
        if (previewMax <= 0) return;

        _ignorePreviewUntil = DateTime.UtcNow.AddMilliseconds(250);
        _previewScroll.Offset = new Vector(_previewScroll.Offset.X, ratio * previewMax);
    }

    private void OnPreviewScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DateTime.UtcNow < _ignorePreviewUntil) return;
        if (_editorScroll == null || _previewScroll == null) return;

        var maxOff = _previewScroll.Extent.Height - _previewScroll.Viewport.Height;
        if (maxOff <= 0) return;
        var ratio = _previewScroll.Offset.Y / maxOff;
        var editorMax = _editorScroll.Extent.Height - _editorScroll.Viewport.Height;
        if (editorMax <= 0) return;

        _ignoreEditorUntil = DateTime.UtcNow.AddMilliseconds(250);
        _editorScroll.Offset = new Vector(_editorScroll.Offset.X, ratio * editorMax);
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
