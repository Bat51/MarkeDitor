using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MarkeDitor.Helpers;
using MarkeDitor.Services;
using MarkeDitor.ViewModels;
using TextMateSharp.Grammars;

namespace MarkeDitor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly FileService _fileService;
    private readonly DialogService _dialogService;
    private readonly RecoveryService _recoveryService;
    private readonly SettingsService _settingsService;
    private EditorBridge? _editor;
    private ScrollSyncHelper? _scrollSync;
    private SpellCheckService? _spell;
    private SpellCheckRenderer? _spellRenderer;
    private WordCompletionProvider? _completion;
    private readonly DebounceHelper _spellDebounce = new(500);
    private readonly DebounceHelper _previewDebounce;
    private readonly DebounceHelper _autoSaveDebounce;
    private readonly string? _initialFilePath;
    private bool _confirmedClose;

    public MainWindow() : this(null) { }

    public MainWindow(string? initialFilePath)
    {
        _initialFilePath = initialFilePath;
        InitializeComponent();

        // Icon is set via XAML (avares://MarkeDitor/Assets/app.png).

        _fileService = new FileService();
        _dialogService = new DialogService(this);
        _viewModel = new MainViewModel(_fileService, _dialogService);
        _recoveryService = new RecoveryService();
        _settingsService = new SettingsService();
        _settingsService.Load();
        _previewDebounce = new DebounceHelper(300);
        _autoSaveDebounce = new DebounceHelper(5000);

        ApplySettings();
        RefreshRecentMenu();

        _editor = new EditorBridge(Editor);
        _editor.ContentChanged += OnEditorContentChanged;
        _editor.CursorPositionChanged += OnCursorPositionChanged;
        _scrollSync = new ScrollSyncHelper(Editor, Preview);

        InitSpellCheck();
        InitAutoCompletion();

        Toolbar.EditorProvider = () => _editor;
        FileExplorer.FileActivated += (_, path) => _ = OpenFileFromExplorerAsync(path);

        Closing += OnWindowClosing;
        Opened += OnWindowOpened;

        // AvaloniaEdit captures keyboard input before the menu's InputGesture
        // can fire, so we intercept app-level shortcuts at the window level
        // using the Tunnel strategy (fires before focused control handlers).
        AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);
    }

    private async void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrl = (e.KeyModifiers & KeyModifiers.Control) != 0;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        if (!ctrl) return;

        switch (e.Key)
        {
            case Key.S when shift:
                e.Handled = true;
                await SaveCurrentFileAs();
                break;
            case Key.S:
                e.Handled = true;
                await SaveCurrentFile();
                break;
            case Key.O when !shift:
                e.Handled = true;
                await OpenFileFromMenuAsync();
                break;
            case Key.N when !shift:
                e.Handled = true;
                CreateNewTab();
                break;
            case Key.W when !shift:
                e.Handled = true;
                if (TabBar.SelectedItem is TabItem ti) await CloseTabAsync(ti);
                break;
            case Key.OemPlus:
            case Key.Add:
                e.Handled = true;
                Zoom(+1);
                break;
            case Key.OemMinus:
            case Key.Subtract:
                e.Handled = true;
                Zoom(-1);
                break;
            case Key.D0:
            case Key.NumPad0:
                e.Handled = true;
                OnZoomReset(this, new Avalonia.Interactivity.RoutedEventArgs());
                break;
            case Key.Space:
                e.Handled = true;
                _completion?.ShowManual();
                break;
            case Key.Tab when shift:
                e.Handled = true;
                SwitchTab(-1);
                break;
            case Key.Tab when !shift:
                e.Handled = true;
                SwitchTab(+1);
                break;
        }
    }

    private void SwitchTab(int delta)
    {
        var count = TabBar.Items.Count;
        if (count <= 1) return;
        var idx = TabBar.SelectedIndex;
        if (idx < 0) idx = 0;
        var next = ((idx + delta) % count + count) % count;
        TabBar.SelectedIndex = next;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_initialFilePath))
        {
            try
            {
                var content = await _fileService.ReadFileAsync(_initialFilePath);
                CreateNewTab(_initialFilePath, content);
                RegisterRecent(_initialFilePath);
                return;
            }
            catch { /* fall through */ }
        }

        var recovered = await TryRecoverFilesAsync();
        if (recovered) return;

        // Reopen last session's tabs if the user enabled the option.
        var reopened = false;
        if (_settingsService.Settings.ReopenLastTabs)
        {
            foreach (var path in _settingsService.Settings.LastOpenTabs)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var content = await _fileService.ReadFileAsync(path);
                    CreateNewTab(path, content);
                    reopened = true;
                }
                catch { /* skip files that can't be re-read */ }
            }
        }
        if (!reopened) CreateNewTab();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Snapshot the open tabs (with file paths) for next launch.
        _settingsService.SaveLastOpenTabs(_viewModel.Tabs.Select(t => t.FilePath));

        if (_confirmedClose) return;

        var dirtyTabs = _viewModel.Tabs.Where(t => t.IsDirty).ToList();
        if (dirtyTabs.Count == 0) return;

        e.Cancel = true;
        _ = PromptUnsavedChangesAndCloseAsync(dirtyTabs);
    }

    private async Task PromptUnsavedChangesAndCloseAsync(List<EditorTabViewModel> dirtyTabs)
    {
        var names = string.Join("\n", dirtyTabs.Select(t => $"• {t.FileName}"));
        var plural = dirtyTabs.Count > 1 ? "s" : string.Empty;
        var title = $"{dirtyTabs.Count} document{plural} non sauvegardé{plural}";
        var content = $"Les documents suivants ont des modifications non enregistrées :\n\n{names}\n\nQue souhaitez-vous faire ?";

        var result = await DialogHelper.ShowYesNoCancelAsync(this, title, content,
            "Tout enregistrer", "Ignorer les modifications", "Annuler");

        if (result == DialogResult.Primary)
        {
            foreach (var tab in dirtyTabs)
            {
                _viewModel.ActiveTab = tab;
                SelectTabItem(tab);
                if (!await SaveTabAsync(tab)) return;
            }
            _confirmedClose = true;
            Close();
        }
        else if (result == DialogResult.Secondary)
        {
            foreach (var tab in _viewModel.Tabs)
                await _recoveryService.DeleteRecoveryAsync(tab.RecoveryId);
            _confirmedClose = true;
            Close();
        }
    }

    private void SelectTabItem(EditorTabViewModel tab)
    {
        foreach (var item in TabBar.Items.OfType<TabItem>())
        {
            if (ReferenceEquals(item.Tag, tab))
            {
                TabBar.SelectedItem = item;
                return;
            }
        }
    }

    private async Task<bool> SaveTabAsync(EditorTabViewModel tab)
    {
        if (tab.FilePath == null)
        {
            var filePath = await _dialogService.ShowSaveFileDialogAsync(tab.FileName);
            if (filePath == null) return false;
            await _fileService.WriteFileAsync(filePath, tab.Content);
            tab.FilePath = filePath;
            tab.FileName = Path.GetFileName(filePath);
        }
        else
        {
            await _fileService.WriteFileAsync(tab.FilePath, tab.Content);
        }
        tab.IsDirty = false;
        UpdateTabDisplay(tab);
        await _recoveryService.DeleteRecoveryAsync(tab.RecoveryId);
        return true;
    }

    private async Task<bool> TryRecoverFilesAsync()
    {
        var recoveries = await _recoveryService.GetPendingRecoveriesAsync();
        if (recoveries.Count == 0) return false;

        var fileList = string.Join("\n", recoveries.Select(r => $"- {r.FileName} ({r.SavedAt:g})"));
        var result = await DialogHelper.ShowYesNoAsync(this,
            "Fichiers non sauvegardés trouvés",
            $"Des fichiers ont été récupérés après une fermeture inattendue :\n\n{fileList}\n\nVoulez-vous les restaurer ?",
            "Restaurer", "Ignorer");

        if (result == DialogResult.Primary)
        {
            foreach (var recovery in recoveries)
            {
                var tab = new EditorTabViewModel
                {
                    RecoveryId = recovery.Id,
                    FilePath = recovery.OriginalPath,
                    FileName = recovery.FileName,
                    Content = recovery.Content,
                    IsDirty = true
                };
                _viewModel.Tabs.Add(tab);
                AddTabItem(tab);
            }
            if (TabBar.Items.Count > 0)
                TabBar.SelectedIndex = 0;
            return true;
        }
        else
        {
            await _recoveryService.CleanupAllAsync();
            return false;
        }
    }

    private void OnEditorContentChanged(object? sender, string content)
    {
        var activeTab = _viewModel.ActiveTab;
        if (activeTab != null)
        {
            activeTab.Content = content;
            activeTab.IsDirty = true;
            UpdateTabDisplay(activeTab);

            var tabToSave = activeTab;
            _autoSaveDebounce.Debounce(() =>
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    try { await _recoveryService.SaveRecoveryAsync(tabToSave); }
                    catch { /* ignore */ }
                });
            });
        }

        _previewDebounce.Debounce(() =>
        {
            Dispatcher.UIThread.Post(() => UpdatePreview(content));
        });

        UpdateWordCount(content);
    }

    private void OnCursorPositionChanged(object? sender, CursorPosition position)
    {
        StatusPosition.Text = $"Ln {position.Line}, Col {position.Column}";
    }

    private void UpdatePreview(string markdownContent)
    {
        Preview.Markdown = markdownContent ?? string.Empty;
    }

    private void UpdateWordCount(string content)
    {
        var wordCount = string.IsNullOrWhiteSpace(content)
            ? 0
            : content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        StatusWordCount.Text = $"{wordCount} words";
    }

    #region Tab Management

    private void CreateNewTab(string? filePath = null, string? content = null)
    {
        var tab = new EditorTabViewModel
        {
            FilePath = filePath,
            FileName = filePath != null ? Path.GetFileName(filePath) : "Untitled",
            Content = content ?? string.Empty,
            IsDirty = false
        };

        _viewModel.Tabs.Add(tab);
        var tabItem = AddTabItem(tab);
        TabBar.SelectedItem = tabItem;
    }

    private TabItem AddTabItem(EditorTabViewModel tab)
    {
        var headerText = new TextBlock
        {
            Text = tab.IsDirty ? $"{tab.FileName} *" : tab.FileName,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        headerText.BindToResource(TextBlock.ForegroundProperty, "AppForegroundBrush");

        var closeButton = new Button
        {
            Content = "×",
            Padding = new Thickness(4, 0),
            MinWidth = 18,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center
        };
        closeButton.BindToResource(Button.ForegroundProperty, "AppMutedTextBrush");
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { headerText, closeButton }
        };
        var tabItem = new TabItem
        {
            Header = headerPanel,
            Tag = tab
        };
        closeButton.Click += async (_, _) => await CloseTabAsync(tabItem);
        TabBar.Items.Add(tabItem);
        return tabItem;
    }

    private async Task CloseTabAsync(TabItem tabItem)
    {
        if (tabItem.Tag is EditorTabViewModel tab && tab.IsDirty)
        {
            var result = await DialogHelper.ShowYesNoCancelAsync(this,
                "Modifications non enregistrées",
                $"Enregistrer les modifications de « {tab.FileName} » ?",
                "Enregistrer", "Ne pas enregistrer", "Annuler");

            if (result == DialogResult.Primary)
            {
                if (!await SaveTabAsync(tab)) return;
            }
            else if (result == DialogResult.Cancel)
            {
                return;
            }
        }

        if (tabItem.Tag is EditorTabViewModel tabToRemove)
        {
            await _recoveryService.DeleteRecoveryAsync(tabToRemove.RecoveryId);
            _viewModel.Tabs.Remove(tabToRemove);
        }
        TabBar.Items.Remove(tabItem);

        if (TabBar.Items.Count == 0)
            CreateNewTab();
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Avalonia raises SelectionChanged during XAML population (before our
        // ctor finishes wiring _editor / _viewModel), so we have to guard.
        if (TabBar == null || _editor == null || _viewModel == null) return;

        if (TabBar.SelectedItem is TabItem selectedTab &&
            selectedTab.Tag is EditorTabViewModel tab)
        {
            _viewModel.ActiveTab = tab;
            _editor.SetContent(tab.Content);
            UpdatePreview(tab.Content);
            UpdateWordCount(tab.Content);
        }
    }

    #endregion

    #region Menu Handlers

    private void OnNewFile(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => CreateNewTab();

    private async void OnOpenFile(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await OpenFileFromMenuAsync();

    private async Task OpenFileFromMenuAsync()
    {
        var filePath = await _dialogService.ShowOpenFileDialogAsync();
        if (filePath != null)
        {
            var content = await _fileService.ReadFileAsync(filePath);
            CreateNewTab(filePath, content);
            RegisterRecent(filePath);
        }
    }

    private async void OnOpenFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folderPath = await _dialogService.ShowOpenFolderDialogAsync();
        if (folderPath != null)
            FileExplorer.LoadFolder(folderPath);
    }

    private async void OnSave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await SaveCurrentFile();

    private async void OnSaveAs(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await SaveCurrentFileAs();

    private void OnExit(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close();

    private void OnUndo(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _editor?.Undo();

    private void OnRedo(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _editor?.Redo();

    private void OnFind(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _editor?.OpenFind();

    private async void OnAbout(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await AboutDialog.ShowAsync(this);

    private async void OnExportHtml(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tab = _viewModel.ActiveTab;
        if (tab == null) return;

        var baseName = Path.GetFileNameWithoutExtension(tab.FileName);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "document";
        var path = await _dialogService.ShowSaveHtmlDialogAsync(baseName + ".html");
        if (string.IsNullOrEmpty(path)) return;

        var md = new MarkdownService();
        var html = md.ToHtmlDocument(tab.Content ?? string.Empty, baseName);
        try
        {
            await File.WriteAllTextAsync(path, html);
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowYesNoAsync(this, "Export failed",
                "Could not write " + path + ":\n" + ex.Message, "OK", null!);
        }
    }

    private async void OnCopyAsHtml(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tab = _viewModel.ActiveTab;
        if (tab == null) return;
        var md = new MarkdownService();
        await PutHtmlOnClipboardAsync(md.ToHtml(tab.Content ?? string.Empty));
    }

    private async void OnCopySelectionAsHtml(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selected = Editor.SelectedText;
        if (string.IsNullOrEmpty(selected)) return;
        var md = new MarkdownService();
        await PutHtmlOnClipboardAsync(md.ToHtml(selected));
    }

    private async Task PutHtmlOnClipboardAsync(string html)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;

        var data = new Avalonia.Input.DataObject();
        // text/html for rich-paste targets (LibreOffice, Gmail, Slack...).
        data.Set("text/html", html);
        // Plain-text fallback so paste into a code editor gets the HTML
        // source rather than nothing.
        data.Set(Avalonia.Input.DataFormats.Text, html);
        await clipboard.SetDataObjectAsync(data);
    }

    #region Settings, Recent files, Zoom

    private void ApplySettings()
    {
        var s = _settingsService.Settings;
        try { Editor.FontFamily = new Avalonia.Media.FontFamily(s.FontFamily); } catch { }
        try { Editor.FontSize = s.FontSize; } catch { }

        var variant = s.Theme switch
        {
            "Light"  => Avalonia.Styling.ThemeVariant.Light,
            "Dark"   => Avalonia.Styling.ThemeVariant.Dark,
            _        => Avalonia.Styling.ThemeVariant.Default,
        };
        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = variant;

        // Sync TextMate code-coloring with the UI theme. For "System", read the
        // resolved variant after assignment so we follow the OS preference.
        var resolved = Application.Current?.ActualThemeVariant;
        var isLight = resolved == Avalonia.Styling.ThemeVariant.Light;
        var tmTheme = isLight ? ThemeName.LightPlus : ThemeName.DarkPlus;
        _editor?.ApplyTheme(tmTheme);
        TextMateCodeBlockPlugin.SetTheme(tmTheme);

        if (_completion != null)
        {
            _completion.Enabled = s.AutoCompleteEnabled;
            _completion.MinChars = s.AutoCompleteMinChars;
        }

        if (_spell != null)
        {
            _spell.Load(s.SpellCheckLanguages, s.CustomDictionary);
            if (s.SpellCheckEnabled)
                _spellRenderer?.Rescan();
            else
                _spellRenderer?.Clear();
        }
    }

    private async void OnPreferences(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var ok = await PreferencesDialog.ShowAsync(this, _settingsService.Settings);
        if (!ok) return;
        _settingsService.Save();
        ApplySettings();
    }

    private void OnZoomIn(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Zoom(+1);
    private void OnZoomOut(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Zoom(-1);
    private void OnZoomReset(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _settingsService.Settings.FontSize = 14;
        _settingsService.Save();
        Editor.FontSize = 14;
    }

    private void Zoom(int delta)
    {
        var current = Editor.FontSize;
        var next = Math.Clamp(current + delta, 8, 48);
        Editor.FontSize = next;
        _settingsService.Settings.FontSize = next;
        _settingsService.Save();
    }

    private void RefreshRecentMenu()
    {
        var menu = MnuRecent;
        menu.Items.Clear();

        var list = _settingsService.Settings.RecentFiles;
        if (list.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "(empty)", IsEnabled = false });
            return;
        }

        foreach (var path in list)
        {
            var item = new MenuItem { Header = path };
            var captured = path;
            item.Click += async (_, _) => await OpenFileFromExplorerAsync(captured);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var clear = new MenuItem { Header = "Clear list" };
        clear.Click += (_, _) => { _settingsService.ClearRecent(); RefreshRecentMenu(); };
        menu.Items.Add(clear);
    }

    private void RegisterRecent(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        _settingsService.RegisterRecent(path);
        RefreshRecentMenu();
    }

    #endregion

    #region Spell check

    private void InitSpellCheck()
    {
        _spell = new SpellCheckService();
        _spell.Load(_settingsService.Settings.SpellCheckLanguages, _settingsService.Settings.CustomDictionary);

        _spellRenderer = new SpellCheckRenderer(Editor, _spell);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_spellRenderer);

        // Right-click on a misspelled word -> suggestions context menu.
        Editor.AddHandler(PointerPressedEvent, OnEditorPointerPressed,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // Trigger an initial scan once the document is loaded.
        Editor.Document.TextChanged += (_, _) => ScheduleSpellRescan();
        ScheduleSpellRescan();
    }

    private void ScheduleSpellRescan()
    {
        if (!_settingsService.Settings.SpellCheckEnabled) return;
        _spellDebounce.Debounce(() =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _spellRenderer?.Rescan()));
    }

    private void OnEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(Editor).Properties;
        if (!props.IsRightButtonPressed) return;
        if (_spell == null || _spellRenderer == null) return;
        if (!_settingsService.Settings.SpellCheckEnabled) return;

        var pos = e.GetPosition(Editor.TextArea.TextView);
        var docPos = Editor.TextArea.TextView.GetPosition(pos);
        if (docPos == null) return;

        var line = Editor.Document.GetLineByNumber(docPos.Value.Line);
        var offset = line.Offset + docPos.Value.Column - 1;
        if (offset < 0 || offset > Editor.Document.TextLength) return;

        var word = _spellRenderer.AtOffset(offset);
        if (word == null) return;

        var menu = new ContextMenu();
        var suggestions = _spell.Suggest(word.Text).ToList();
        if (suggestions.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "(no suggestions)", IsEnabled = false });
        }
        else
        {
            foreach (var s in suggestions)
            {
                var item = new MenuItem { Header = s };
                var captured = s;
                var w = word;
                item.Click += (_, _) =>
                {
                    Editor.Document.Replace(w.Offset, w.Length, captured);
                    ScheduleSpellRescan();
                };
                menu.Items.Add(item);
            }
        }
        menu.Items.Add(new Separator());
        var add = new MenuItem { Header = $"Add \"{word.Text}\" to dictionary" };
        var capturedWord = word.Text;
        add.Click += (_, _) =>
        {
            _settingsService.AddToCustomDictionary(capturedWord);
            _spell?.AddToCustom(capturedWord);
            _spellRenderer?.Rescan();
        };
        menu.Items.Add(add);

        menu.PlacementTarget = Editor;
        menu.Open(Editor);
        e.Handled = true;
    }

    #endregion

    #region Auto-completion

    private void InitAutoCompletion()
    {
        _completion = new WordCompletionProvider(Editor)
        {
            Enabled = _settingsService.Settings.AutoCompleteEnabled,
            MinChars = _settingsService.Settings.AutoCompleteMinChars,
        };
    }

    #endregion

    private void OnToggleFileExplorer(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var col = MainContentGrid.ColumnDefinitions[0];
        col.Width = col.Width.Value > 0 || col.Width.IsStar
            ? new GridLength(0)
            : new GridLength(250);
    }

    private void OnTogglePreview(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var col = EditorPreviewGrid.ColumnDefinitions[2];
        col.Width = col.Width.Value > 0 || col.Width.IsStar
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
    }

    #endregion

    #region File Operations

    private async Task SaveCurrentFile()
    {
        var tab = _viewModel.ActiveTab;
        if (tab == null) return;

        if (tab.FilePath == null) { await SaveCurrentFileAs(); return; }

        await _fileService.WriteFileAsync(tab.FilePath, tab.Content);
        tab.IsDirty = false;
        UpdateTabDisplay(tab);
        await _recoveryService.DeleteRecoveryAsync(tab.RecoveryId);
        RegisterRecent(tab.FilePath);
    }

    private async Task SaveCurrentFileAs()
    {
        var tab = _viewModel.ActiveTab;
        if (tab == null) return;

        var filePath = await _dialogService.ShowSaveFileDialogAsync(tab.FileName);
        if (filePath != null)
        {
            await _fileService.WriteFileAsync(filePath, tab.Content);
            tab.FilePath = filePath;
            tab.FileName = Path.GetFileName(filePath);
            tab.IsDirty = false;
            UpdateTabDisplay(tab);
            await _recoveryService.DeleteRecoveryAsync(tab.RecoveryId);
            RegisterRecent(filePath);
        }
    }

    private void UpdateTabDisplay(EditorTabViewModel tab)
    {
        foreach (var item in TabBar.Items.OfType<TabItem>())
        {
            if (ReferenceEquals(item.Tag, tab) &&
                item.Header is StackPanel sp &&
                sp.Children.Count > 0 &&
                sp.Children[0] is TextBlock tb)
            {
                tb.Text = tab.IsDirty ? $"{tab.FileName} *" : tab.FileName;
                break;
            }
        }
    }

    #endregion

    private async Task OpenFileFromExplorerAsync(string filePath)
    {
        foreach (var existingTab in _viewModel.Tabs)
        {
            if (existingTab.FilePath == filePath)
            {
                SelectTabItem(existingTab);
                return;
            }
        }

        var content = await _fileService.ReadFileAsync(filePath);
        CreateNewTab(filePath, content);
        RegisterRecent(filePath);
    }
}
