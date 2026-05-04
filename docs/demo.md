# Welcome to MarkeDitor

A **cross-platform** Markdown editor with *live preview*, written in
C# and [Avalonia](https://avaloniaui.net/). Type on the left, see the
rendered output on the right — `instantly`.

## Highlights

- **Native** rendering — no embedded browser
- **Syntax-highlighted** code blocks in the preview
- **Bidirectional** scroll sync + click-in-preview-jumps-to-line
- **Spell check** in French + English (Hunspell)
- **Auto-completion** trained on the current document

## Shortcuts

| Shortcut          | Action                  |
|-------------------|-------------------------|
| `Ctrl+S`          | Save                    |
| `Ctrl+B` / `Ctrl+I` | Bold / Italic         |
| `Ctrl++` / `Ctrl+-` | Zoom in / out         |
| `Ctrl+Space`      | Force auto-complete     |
| `Ctrl+F`          | Find in current file    |

## Code blocks are highlighted per language

```python
def fibonacci(n):
    a, b = 0, 1
    for _ in range(n):
        yield a
        a, b = b, a + b

print(list(fibonacci(10)))
# [0, 1, 1, 2, 3, 5, 8, 13, 21, 34]
```

```csharp
public static IEnumerable<int> Fibonacci(int n)
{
    int a = 0, b = 1;
    for (var i = 0; i < n; i++)
    {
        yield return a;
        (a, b) = (b, a + b);
    }
}
```

> "The best Markdown editor is the one that gets out of your way."

---

*Open this file in MarkeDitor to see the preview side-by-side.*
