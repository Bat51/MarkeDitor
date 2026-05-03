using System;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Search;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace MarkeDitor.Helpers;

public class CursorPosition
{
    public int Line { get; set; }
    public int Column { get; set; }
}

/// <summary>
/// Wraps an AvaloniaEdit TextEditor to expose the same surface that the
/// previous Monaco-in-WebView integration offered: content change events,
/// cursor position, undo/redo, find, programmatic edits.
/// </summary>
public class EditorBridge
{
    private readonly TextEditor _editor;
    private bool _suppressContentChanged;

    public event EventHandler<string>? ContentChanged;
    public event EventHandler<CursorPosition>? CursorPositionChanged;

    public EditorBridge(TextEditor editor)
    {
        _editor = editor;
        SearchPanel.Install(editor);

        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        var textMate = editor.InstallTextMate(registryOptions);
        textMate.SetGrammar(registryOptions.GetScopeByLanguageId("markdown"));

        editor.TextChanged += (_, _) =>
        {
            if (_suppressContentChanged) return;
            ContentChanged?.Invoke(this, editor.Text);
        };

        editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            var caret = editor.TextArea.Caret;
            CursorPositionChanged?.Invoke(this,
                new CursorPosition { Line = caret.Line, Column = caret.Column });
        };
    }

    public void SetContent(string content)
    {
        _suppressContentChanged = true;
        try { _editor.Text = content ?? string.Empty; }
        finally { _suppressContentChanged = false; }
    }

    public string GetContent() => _editor.Text;

    public void InsertText(string text)
    {
        var doc = _editor.Document;
        var selStart = _editor.SelectionStart;
        var selLen = _editor.SelectionLength;

        if (selLen > 0)
            doc.Replace(selStart, selLen, text);
        else
            doc.Insert(_editor.CaretOffset, text);

        _editor.Focus();
    }

    public void WrapSelection(string before, string after)
    {
        var doc = _editor.Document;
        var selStart = _editor.SelectionStart;
        var selLen = _editor.SelectionLength;
        var selectedText = selLen > 0 ? doc.GetText(selStart, selLen) : string.Empty;
        var placeholder = string.IsNullOrEmpty(selectedText) ? "text" : selectedText;
        var replacement = before + placeholder + after;

        if (selLen > 0)
            doc.Replace(selStart, selLen, replacement);
        else
            doc.Insert(_editor.CaretOffset, replacement);

        if (string.IsNullOrEmpty(selectedText))
        {
            // Select the placeholder so the user can overwrite it.
            _editor.SelectionStart = selStart + before.Length;
            _editor.SelectionLength = placeholder.Length;
        }

        _editor.Focus();
    }

    public void InsertAtLineStart(string prefix)
    {
        var line = _editor.Document.GetLineByOffset(_editor.CaretOffset);
        _editor.Document.Insert(line.Offset, prefix);
        _editor.Focus();
    }

    public void Undo() => _editor.Undo();
    public void Redo() => _editor.Redo();
    public void OpenFind() => SearchPanel.Install(_editor).Open();

    public void RevealLine(int line)
    {
        if (line < 1) line = 1;
        if (line > _editor.Document.LineCount) line = _editor.Document.LineCount;
        var docLine = _editor.Document.GetLineByNumber(line);
        _editor.ScrollToLine(line);
        _editor.CaretOffset = docLine.Offset;
    }

    public void SetCursorLine(int line)
    {
        RevealLine(line);
        _editor.Focus();
    }
}
