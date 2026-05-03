using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MarkeDitor.Helpers;

public static class AboutDialog
{
    public static Task ShowAsync(Window owner)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        var dialog = new Window
        {
            Title = "About MarkeDitor",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(0x2d, 0x33, 0x3b))
        };

        var icon = new Image
        {
            Width = 96,
            Height = 96,
            Margin = new Thickness(0, 4, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        try
        {
            using var stream = AssetLoader.Open(new System.Uri("avares://MarkeDitor/Assets/app.png"));
            icon.Source = new Bitmap(stream);
        }
        catch { /* no icon, no big deal */ }

        var fg = new SolidColorBrush(Color.FromRgb(0xcd, 0xd9, 0xe5));
        var muted = new SolidColorBrush(Color.FromRgb(0x76, 0x83, 0x90));

        var name = new TextBlock
        {
            Text = "MarkeDitor",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = fg
        };
        var ver = new TextBlock
        {
            Text = $"Version {version}",
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = muted,
            Margin = new Thickness(0, 4, 0, 8)
        };
        var desc = new TextBlock
        {
            Text = "Cross-platform Markdown editor with live preview.\n" +
                   "Built with Avalonia 11, AvaloniaEdit and Markdig.",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Foreground = fg,
            Margin = new Thickness(20, 0, 20, 16)
        };

        var ok = new Button
        {
            Content = "Close",
            MinWidth = 90,
            IsDefault = true,
            IsCancel = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        };
        ok.Click += (_, _) => dialog.Close();

        var panel = new StackPanel
        {
            Margin = new Thickness(20, 16, 20, 0),
            Children = { icon, name, ver, desc, ok }
        };
        dialog.Content = panel;

        return dialog.ShowDialog(owner);
    }
}
