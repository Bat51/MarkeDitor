using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MarkeDitor.Services;

namespace MarkeDitor.Helpers;

public static class PreferencesDialog
{
    private static readonly string[] FontFamilies =
    {
        "Cascadia Code,Consolas,Menlo,Monospace",
        "DejaVu Sans Mono,Monospace",
        "Liberation Mono,Monospace",
        "Ubuntu Mono,Monospace",
        "JetBrains Mono,Monospace",
        "Fira Code,Monospace",
        "Source Code Pro,Monospace",
        "Inter,Sans-Serif",
        "Sans-Serif",
    };

    private static readonly double[] FontSizes = { 10, 11, 12, 13, 14, 15, 16, 17, 18, 20, 22, 24 };

    private static readonly string[] Themes = { "Dark", "Light", "System" };

    public static async Task<bool> ShowAsync(Window owner, AppSettings settings)
    {
        var fg = new SolidColorBrush(Color.FromRgb(0xcd, 0xd9, 0xe5));
        var bg = new SolidColorBrush(Color.FromRgb(0x2d, 0x33, 0x3b));

        var dialog = new Window
        {
            Title = "Preferences",
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = bg,
        };

        var fontCombo = new ComboBox { ItemsSource = FontFamilies, SelectedItem = settings.FontFamily, Margin = new Thickness(0, 4, 0, 12), MinWidth = 260 };
        if (fontCombo.SelectedItem == null) fontCombo.SelectedItem = FontFamilies[0];

        var sizeCombo = new ComboBox { ItemsSource = FontSizes, SelectedItem = settings.FontSize, Margin = new Thickness(0, 4, 0, 12), MinWidth = 80 };
        if (sizeCombo.SelectedItem == null) sizeCombo.SelectedItem = 14.0;

        var themeCombo = new ComboBox { ItemsSource = Themes, SelectedItem = settings.Theme, Margin = new Thickness(0, 4, 0, 12), MinWidth = 120 };
        if (themeCombo.SelectedItem == null) themeCombo.SelectedItem = "Dark";

        var reopen = new CheckBox { Content = "Reopen last tabs on startup", IsChecked = settings.ReopenLastTabs, Foreground = fg, Margin = new Thickness(0, 8, 0, 4) };
        var autocomplete = new CheckBox { Content = "Auto-complete words while typing", IsChecked = settings.AutoCompleteEnabled, Foreground = fg, Margin = new Thickness(0, 4, 0, 4) };
        var spell = new CheckBox { Content = "Spell check (FR + EN)", IsChecked = settings.SpellCheckEnabled, Foreground = fg, Margin = new Thickness(0, 4, 0, 8) };

        var grid = new Grid
        {
            Margin = new Thickness(20, 16, 20, 0),
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto"),
        };
        Add(grid, new TextBlock { Text = "Font family", Foreground = fg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 12, 12) }, 0, 0);
        Add(grid, fontCombo, 0, 1);
        Add(grid, new TextBlock { Text = "Font size", Foreground = fg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 12, 12) }, 1, 0);
        Add(grid, sizeCombo, 1, 1);
        Add(grid, new TextBlock { Text = "Theme", Foreground = fg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 12, 12) }, 2, 0);
        Add(grid, themeCombo, 2, 1);
        Grid.SetColumnSpan(reopen, 2);
        Grid.SetColumnSpan(autocomplete, 2);
        Grid.SetColumnSpan(spell, 2);
        Add(grid, reopen, 3, 0);
        Add(grid, autocomplete, 4, 0);
        Add(grid, spell, 5, 0);

        var ok = new Button { Content = "Save", MinWidth = 90, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true };
        var saved = false;
        ok.Click += (_, _) =>
        {
            settings.FontFamily = (string)fontCombo.SelectedItem!;
            settings.FontSize = (double)sizeCombo.SelectedItem!;
            settings.Theme = (string)themeCombo.SelectedItem!;
            settings.ReopenLastTabs = reopen.IsChecked == true;
            settings.AutoCompleteEnabled = autocomplete.IsChecked == true;
            settings.SpellCheckEnabled = spell.IsChecked == true;
            saved = true;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12),
            Spacing = 8,
            Children = { ok, cancel }
        };

        var root = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(grid);
        dialog.Content = root;

        await dialog.ShowDialog(owner);
        return saved;
    }

    private static void Add(Grid grid, Control child, int row, int col)
    {
        Grid.SetRow(child, row);
        Grid.SetColumn(child, col);
        grid.Children.Add(child);
    }
}
