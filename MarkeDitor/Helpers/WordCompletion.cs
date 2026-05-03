using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace MarkeDitor.Helpers;

public class WordCompletionData : ICompletionData
{
    public WordCompletionData(string text) { Text = text; }
    public IImage? Image => null;
    public string Text { get; }
    public object Content => Text;
    public object? Description => null;
    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}

/// <summary>
/// Triggers AvaloniaEdit's CompletionWindow with words extracted from the
/// current document once the user has typed a configurable minimum number
/// of characters at the end of a word.
/// </summary>
public class WordCompletionProvider
{
    private readonly TextEditor _editor;
    private CompletionWindow? _window;
    public bool Enabled { get; set; } = true;
    public int MinChars { get; set; } = 4;

    public WordCompletionProvider(TextEditor editor)
    {
        _editor = editor;
        _editor.TextArea.TextEntered += OnTextEntered;
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (!Enabled) return;
        if (_window != null) return;                          // already showing
        var ch = e.Text;
        if (string.IsNullOrEmpty(ch) || !char.IsLetter(ch[0])) return;
        TryShow(triggeredManually: false);
    }

    public void ShowManual()
    {
        if (_window != null) return;
        TryShow(triggeredManually: true);
    }

    private void TryShow(bool triggeredManually)
    {
        var (start, prefix) = GetWordPrefixBeforeCaret();
        if (prefix.Length == 0) return;
        if (!triggeredManually && prefix.Length < MinChars) return;

        var suggestions = ExtractWords(_editor.Document.Text, prefix)
            .Take(50)
            .ToList();
        if (suggestions.Count == 0) return;

        var win = new CompletionWindow(_editor.TextArea)
        {
            CloseAutomatically = true,
            CloseWhenCaretAtBeginning = true,
            StartOffset = start,
            EndOffset = _editor.CaretOffset,
        };
        var data = win.CompletionList.CompletionData;
        foreach (var w in suggestions)
            data.Add(new WordCompletionData(w));

        win.Closed += (_, _) => _window = null;
        _window = win;
        win.Show();
    }

    private (int start, string prefix) GetWordPrefixBeforeCaret()
    {
        var doc = _editor.Document;
        var caret = _editor.CaretOffset;
        var start = caret;
        while (start > 0 && IsWordChar(doc.GetCharAt(start - 1)))
            start--;
        return (start, doc.GetText(start, caret - start));
    }

    private static bool IsWordChar(char c) =>
        char.IsLetterOrDigit(c) || c == '\'' || c == '’' || c == '-' || c == '_';

    private static IEnumerable<string> ExtractWords(string text, string prefix)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        seen.Add(prefix); // don't suggest the user's own current input

        var i = 0;
        while (i < text.Length)
        {
            if (!char.IsLetter(text[i])) { i++; continue; }
            var s = i;
            while (i < text.Length && IsWordChar(text[i])) i++;
            var len = i - s;
            if (len <= prefix.Length) continue;
            var word = text.Substring(s, len);
            if (!word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            // Preserve case of the source occurrence; users rarely want to be told
            // their lowercase prefix match is identical except for case.
            if (seen.Add(word)) yield return word;
        }
    }
}
