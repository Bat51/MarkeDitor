# MarkeDitor — project handoff for Claude

This file is for any future Claude Code session that opens this directory.
It captures the technical decisions and gotchas accumulated during the port
from Windows (WinUI 3) to a cross-platform Avalonia 11 codebase, so you
don't have to rediscover them.

## What MarkeDitor is

Cross-platform Markdown editor with live preview (Linux + Windows + macOS),
single Avalonia 11 / .NET 8 codebase. Public repo:
<https://github.com/Bat51/MarkeDitor>.

Originally a WinUI 3 app using Monaco-in-WebView2; the WebView story on
modern Linux turned out to be a dead end (see "Why no WebView" below), so the
preview is now native Avalonia.

## Build, run, install

```sh
cd MarkeDitor && dotnet run                                  # dev
./publish-linux.sh                                            # → Publish/MarkeDitor-linux-x64/
./install-linux.sh                                            # per-user install (no sudo)
./uninstall-linux.sh
./publish-windows.sh                                          # cross-build win-x64 .exe
# Inno Setup against installer.iss on Windows for a real installer
```

The Linux install script is the canonical path; it also registers the
.desktop entry and icon so GNOME shows MarkeDitor in Activities.

User runtime state lives in `~/.local/share/MarkeDitor/`:

- `settings.json` — preferences, recent files, last open tabs, custom dictionary
- `Recovery/` — auto-saved snapshots of unsaved tabs

## Architecture map

```
MarkeDitor/
├── App.axaml(.cs)                      # app root, theme, command-line file arg
├── Program.cs                          # Avalonia entry point
├── MainWindow.axaml(.cs)               # ~700 lines, the bulk of the wiring
├── Models/FileItem.cs                  # tree node for the file explorer
├── ViewModels/                         # MVVM (CommunityToolkit.Mvvm)
│   ├── MainViewModel.cs
│   ├── EditorTabViewModel.cs
│   └── FileExplorerViewModel.cs
├── Services/
│   ├── FileService.cs                  # File.Read/WriteAllTextAsync wrapper
│   ├── DialogService.cs                # IStorageProvider (cross-platform)
│   ├── MarkdownService.cs              # Markdig HTML rendering (kept for future export)
│   ├── RecoveryService.cs              # snapshot dirty tabs to LocalApplicationData
│   ├── SettingsService.cs              # JSON persistence, RegisterRecent, custom dict
│   └── SpellCheckService.cs            # WeCantSpell.Hunspell wrapper
├── Helpers/
│   ├── EditorBridge.cs                 # AvaloniaEdit wrapper exposing the API
│   │                                   # the toolbar/menu used to call on the old
│   │                                   # MonacoInterop. TextMate Markdown grammar
│   │                                   # is installed here.
│   ├── ScrollSyncHelper.cs             # bidirectional ratio-based scroll sync
│   │                                   # + click-in-preview-jumps-to-line
│   ├── WordCompletion.cs               # AvaloniaEdit CompletionWindow with words
│   │                                   # from the current document
│   ├── SpellCheckRenderer.cs           # IBackgroundRenderer drawing red wavy
│   │                                   # underlines + WordTokenizer
│   ├── CodeBlockHighlighter.cs         # custom Markdown.Avalonia plugin: replaces
│   │                                   # fenced code block rendering with a
│   │                                   # TextMate-highlighted AvaloniaEdit
│   ├── DialogHelper.cs                 # 2/3-button modal dialogs
│   ├── PreferencesDialog.cs            # the settings window
│   ├── AboutDialog.cs                  # the about window
│   └── DebounceHelper.cs               # cancellable delay
└── Views/
    ├── FileExplorerView.axaml(.cs)     # TreeView, exposes FileActivated event
    └── ToolbarView.axaml(.cs)          # B/I/H1-H3/links/code/lists buttons
```

## Pinned versions (don't bump blindly)

- **Avalonia 11.0.10** — pinned. Markdown.Avalonia 11.0.2 uses the internal
  `StaticBinding` value type which Avalonia 11.3+ rejects with
  `NotSupportedException` at runtime in `UpdatePreview`. If you upgrade
  Avalonia, replace Markdown.Avalonia first.
- **AvaloniaEdit 11.0.6** — must match Avalonia major.minor.
- **Markdown.Avalonia 11.0.2** — only published version; latest as of 2026.
- **WeCantSpell.Hunspell 7.0.1** — MIT, pure managed. Do *not* swap to
  NHunspell (LGPL).

## Why no WebView

The original plan was Avalonia + WebView (keep Monaco). On Ubuntu 26.04 we
hit dead ends in this order:

1. **WebView.Avalonia 11.0.0.1** (`libwebkit2gtk-4.0.so.37`): the package
   targets a SONAME the distro no longer ships (only `libwebkit2gtk-4.1`).
2. **Symlink 4.1 → 4.0**: loads, then segfaults during init. ABI is too
   different (libsoup 2 vs 3 underneath).
3. **WebView.Avalonia.Cross 11.3.1**: same hardcoded SONAME, same problem.
4. **WebViewControl-Avalonia (CefGlue 120)**: ships its own Chromium (~380 MB
   in the package, ~100 MB in published output), but segfaults during CEF
   bring-up on this system, after sandbox + libsecret init.

So the preview now uses native Avalonia controls (`Markdown.Avalonia`) and
the editor uses native AvaloniaEdit. **Don't try to bring back WebView**
without first verifying WebKit2GTK 4.0 is somehow available, or finding a
CefGlue version that works on the target distro. Time sink confirmed.

## Subtle gotchas worth remembering

### Source generators must be visible

`x:Name="Foo"` produces a field `Foo` only when both:
- the class is `partial` and matches `x:Class`
- the auto-generated `InitializeComponent` is *not* shadowed by a manual
  `InitializeComponent() => AvaloniaXamlLoader.Load(this)` you wrote yourself

If you write your own `InitializeComponent`, the AvaloniaNameSourceGenerator
still emits the field declarations, but they never get assigned — the
runtime crashes with `NullReferenceException` on first access. The fix is
to delete the manual override and let the generator win. Setting
`<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` makes the
generated `*.g.cs` visible under `obj/Generated/` for debugging.

### TabControl raises SelectionChanged during XAML population

`OnTabSelectionChanged` is called *before* the constructor body finishes
wiring `_editor` / `_viewModel`. All event handlers must guard against null
fields. Same pattern can affect any Selector-derived control.

### AvaloniaEdit eats Ctrl+S (and friends)

The TextEditor has its own keyboard handling; menu `InputGesture="Ctrl+S"`
never fires when focus is in the editor. The window-level
`OnGlobalKeyDown` (`AddHandler(KeyDownEvent, ..., RoutingStrategies.Tunnel)`)
intercepts shortcuts before the editor sees them. Add new shortcuts there,
not on menu items.

### Linux paths and the file-association bug

`File → Open with` on Linux passes `/home/.../file.md` as `argv[1]`. The
original code skipped any arg starting with `/` (Windows switch convention),
which meant double-clicking a `.md` file opened a blank tab. Guard:

```csharp
if (arg.StartsWith("-")) continue;
if (OperatingSystem.IsWindows() && arg.StartsWith("/")) continue;
```

### GNOME doesn't show window icons in the title bar

`Icon="avares://MarkeDitor/Assets/app.png"` in the Window XAML *does* set
`_NET_WM_ICON` (verifiable with `xprop`). GNOME Shell ignores it for the
title bar; the Activities / taskbar entry uses the `.desktop` file's `Icon=`
plus `StartupWMClass=MarkeDitor`. The install script handles all of that.

### Markdown.Avalonia.Full vs Tight

The XAML namespace `xmlns:md="https://github.com/whistyun/Markdown.Avalonia"`
resolves to `Markdown.Avalonia.Full.MarkdownScrollViewer` once the Full
package is referenced (which we want — it pulls in HTML, SVG, etc.
extensions). The base type lives in `Markdown.Avalonia.Tight.dll`. Plugin
overrides for code blocks have to be wired through `<md:MdAvPlugins>`, not
manipulated directly on the scroll viewer.

### Custom code block plugin

`Helpers/CodeBlockHighlighter.cs` registers an `IBlockOverride` that
intercepts fenced code blocks and renders them with a read-only
AvaloniaEdit + TextMate. We rolled our own because
`Markdown.Avalonia.SyntaxHigh` 11.0.2 produces colors invisible on dark
themes. The regex is matched against the raw markdown source, then the
matched language and code go through `MapLanguage` to the TextMate scope.
Add aliases in `MapLanguage` if a language doesn't pick up the right
grammar (e.g. `js → javascript`).

### Hunspell dictionaries

We load `fr_FR` and `en_US` from `/usr/share/hunspell/`. A word counts as
correct if any loaded dictionary or the user's custom dictionary accepts it.
On systems missing the dictionaries, spell-check silently does nothing
(`SpellCheckService.IsReady` is false).

`apt install hunspell-fr hunspell-en-us` on Ubuntu/Debian if needed.

## What's intentionally not done

- Sync scroll uses a **ratio**, not source-line → preview-block mapping.
  The Markdig `data-line` injection in `MarkdownService` is still there for
  a future HTML export but is unused by the preview. Improving precision
  would require walking the rendered FlowDocument and tagging blocks with
  line ranges; we judged it not worth it.
- The "Reset zoom" hard-codes 14 — fine, since it's the default font size.
- `MarkdownService.ToHtml()` is unused at runtime. Kept because it's correct
  and useful for any future "Export to HTML" feature.
- macOS RIDs (`osx-x64`, `osx-arm64`) are listed in the csproj but the build
  has never been validated on macOS.

## Tasks the user might come back with

If the user asks for any of these, they're already on the implicit roadmap
and ordered roughly by ease:

- **Find & replace** in the editor — AvaloniaEdit's `SearchPanel.Install`
  is already wired in `EditorBridge`, only Find is exposed on the menu;
  Replace is one menu item away.
- **Print / export PDF** — Avalonia has no print API; would need
  `Avalonia.Print.Embedded` or render preview to PDF via PdfSharp.
- **Multiple cursors / column selection** — AvaloniaEdit supports it
  natively, just needs UI glue.
- **Spell-check language detection per file** — we currently load all
  configured dictionaries simultaneously; could be smarter.
- **HTML export** — `MarkdownService.ToHtml(content)` already does the
  Markdig conversion with `data-line` attributes; just needs File → Export.

## How to keep this current

If you (Claude or the user) ship a non-trivial change that fights against
something documented here, update this file in the same commit. The whole
point is that the next session doesn't relearn the same lessons.
