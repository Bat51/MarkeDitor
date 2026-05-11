using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using MarkeDitor.Services;

namespace MarkeDitor.Helpers;

/// <summary>
/// Modal for editing the CSS that ships embedded with HTML exports.
/// The text area is monospaced and pre-fills with the user's custom
/// stylesheet, or the built-in default when none has been saved yet.
/// </summary>
public static class CssEditorDialog
{
    public static async Task<bool> ShowAsync(Window owner, AppSettings settings)
    {
        var dialog = new Window
        {
            Title = "Edit export CSS",
            Width = 760,
            Height = 560,
            CanResize = true,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        dialog.BindToResource(Window.BackgroundProperty, "AppPanelBrush");

        var preface = new TextBlock
        {
            Text = "This stylesheet is embedded in every File → Export as HTML output. "
                 + "Leave it identical to the default if you don't need to customise the rendering.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(16, 16, 16, 8),
        };
        preface.BindToResource(TextBlock.ForegroundProperty, "AppMutedTextBrush");

        var initial = string.IsNullOrWhiteSpace(settings.CustomExportCss)
            ? MarkdownService.DefaultCss
            : settings.CustomExportCss!;

        var editor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Cascadia Code,Consolas,Menlo,DejaVu Sans Mono,Monospace"),
            FontSize = 12,
            Margin = new Thickness(16, 0, 16, 8),
            Text = initial,
        };
        editor.BindToResource(TextBox.BackgroundProperty, "AppCodeBackgroundBrush");
        editor.BindToResource(TextBox.ForegroundProperty, "AppForegroundBrush");

        var editorScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = editor,
        };

        var saved = false;
        var save = new Button { Content = "Save", MinWidth = 90, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true };
        var reset = new Button { Content = "Reset to default", MinWidth = 140 };

        save.Click += (_, _) =>
        {
            // Treat "equal to default" as "no override" so users who just
            // want to peek at the CSS don't accidentally fossilise the
            // current default.
            settings.CustomExportCss =
                string.Equals(editor.Text?.TrimEnd(), MarkdownService.DefaultCss.TrimEnd(),
                              System.StringComparison.Ordinal)
                ? null
                : editor.Text;
            saved = true;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        reset.Click += (_, _) => editor.Text = MarkdownService.DefaultCss;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12),
            Spacing = 8,
            Children = { reset, save, cancel }
        };

        var root = new DockPanel();
        DockPanel.SetDock(preface, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(preface);
        root.Children.Add(buttons);
        root.Children.Add(editorScroll);

        dialog.Content = root;
        await dialog.ShowDialog(owner);
        return saved;
    }
}
