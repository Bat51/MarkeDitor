using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Markdown.Avalonia;

namespace MarkeDitor.Helpers;

/// <summary>
/// Bidirectional scroll sync between AvaloniaEdit and Markdown.Avalonia.
///
/// Editor -> preview is straightforward: any change in the editor's scroll
/// position propagates to the preview proportionally.
///
/// Preview -> editor is the tricky direction. Markdown.Avalonia rebuilds
/// its content tree on every <c>Markdown</c> assignment and its inner
/// ScrollViewer emits a long tail of ScrollChanged events as the layout
/// settles (offset clamps to 0 mid-rebuild, then extent grows back over
/// several frames, sometimes more if images/embedded SVGs load async).
/// Honouring those would yank the editor — typically straight back to the
/// top of the document — every time the user types.
///
/// We therefore treat preview -> editor sync as opt-in: it only fires when
/// the user has just acted on the preview (mouse wheel or scrollbar/track
/// click). Programmatic offset changes from rebuilds are silently
/// swallowed. Editor -> preview alignment is also re-applied on each
/// preview layout pass for a short window after typing, so the preview
/// catches up to the editor's position once the rebuild settles.
/// </summary>
public class ScrollSyncHelper
{
    private readonly TextEditor _editor;
    private readonly MarkdownScrollViewer _preview;
    private ScrollViewer? _editorScroll;
    private ScrollViewer? _previewScroll;

    private DateTime _ignoreEditorUntil = DateTime.MinValue;
    private DateTime _ignorePreviewUntil = DateTime.MinValue;
    private DateTime _previewUserScrollUntil = DateTime.MinValue;
    private DateTime _resyncPreviewUntil = DateTime.MinValue;

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
        // While Markdown.Avalonia rebuilds the preview after this edit, its
        // ScrollChanged events are not user-initiated and must not move the
        // editor. We also re-apply editor->preview on each subsequent
        // layout pass for a short window so the preview ends up where the
        // caret is rather than stuck at the top.
        var now = DateTime.UtcNow;
        _ignorePreviewUntil = now.AddMilliseconds(1500);
        _resyncPreviewUntil = now.AddMilliseconds(1500);
    }

    private void OnPreviewLayoutUpdated(object? sender, EventArgs e)
    {
        TryHookPreview();
        if (DateTime.UtcNow < _resyncPreviewUntil)
            ApplyEditorRatioToPreview();
    }

    private void OnPreviewWheel(object? sender, PointerWheelEventArgs e)
    {
        // Whitelist preview -> editor sync for a brief window after a real
        // user interaction. The ScrollChanged event that follows the wheel
        // tick will then be honoured.
        _previewUserScrollUntil = DateTime.UtcNow.AddMilliseconds(400);
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
        ApplyEditorRatioToPreview();
    }

    private void OnPreviewScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DateTime.UtcNow < _ignorePreviewUntil) return;
        // Only honour preview -> editor when the user just acted on the
        // preview. Rebuild-driven scroll changes never reach this point.
        if (DateTime.UtcNow >= _previewUserScrollUntil) return;
        if (_editorScroll == null || _previewScroll == null) return;

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
        var maxOff = _editorScroll.Extent.Height - _editorScroll.Viewport.Height;
        if (maxOff <= 0) return;
        var ratio = _editorScroll.Offset.Y / maxOff;
        var previewMax = _previewScroll.Extent.Height - _previewScroll.Viewport.Height;
        if (previewMax <= 0) return;

        // Push the ignore window forward so any ScrollChanged the assignment
        // below produces does not echo back to the editor.
        var bumped = DateTime.UtcNow.AddMilliseconds(300);
        if (bumped > _ignorePreviewUntil) _ignorePreviewUntil = bumped;
        _previewScroll.Offset = new Vector(_previewScroll.Offset.X, ratio * previewMax);
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
