using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace MarkeDitor.Helpers;

/// <summary>
/// Small modal that shows a search box and a wrap-grid of clickable emoji
/// tiles. Returns the chosen shortcode (e.g. ":smile:") or <c>null</c> if
/// the user cancels.
/// </summary>
public static class EmojiPickerDialog
{
    public static async Task<string?> ShowAsync(Window owner)
    {
        var dialog = new Window
        {
            Title = "Insert emoji",
            Width = 460,
            Height = 480,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        dialog.BindToResource(Window.BackgroundProperty, "AppPanelBrush");

        string? result = null;

        var search = new TextBox
        {
            Watermark = "Search by name…",
            Margin = new Thickness(12, 12, 12, 8),
        };

        var grid = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 0, 8, 8),
        };

        Button MakeTile(EmojiCatalog.Entry entry)
        {
            var btn = new Button
            {
                Content = new TextBlock
                {
                    Text = entry.Glyph,
                    FontSize = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                Width = 44,
                Height = 44,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
            };
            btn.BindToResource(Button.BorderBrushProperty, "AppDividerBrush");
            ToolTip.SetTip(btn, entry.Shortcode);
            btn.Click += (_, _) =>
            {
                result = entry.Shortcode;
                dialog.Close();
            };
            return btn;
        }

        void Populate(string filter)
        {
            grid.Children.Clear();
            IEnumerable<EmojiCatalog.Entry> source = EmojiCatalog.All;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                var f = filter.Trim().Trim(':').ToLowerInvariant();
                source = source.Where(e =>
                    e.Shortcode.ToLowerInvariant().Contains(f) ||
                    e.Category.ToLowerInvariant().Contains(f));
            }
            foreach (var e in source) grid.Children.Add(MakeTile(e));
        }

        Populate(string.Empty);
        search.TextChanged += (_, _) => Populate(search.Text ?? string.Empty);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = grid,
        };

        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true, Margin = new Thickness(12) };
        cancel.Click += (_, _) => dialog.Close();
        var bottom = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel }
        };

        var root = new DockPanel();
        DockPanel.SetDock(search, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(search);
        root.Children.Add(bottom);
        root.Children.Add(scroll);

        dialog.Content = root;
        dialog.Opened += (_, _) => search.Focus();

        await dialog.ShowDialog(owner);
        return result;
    }
}
