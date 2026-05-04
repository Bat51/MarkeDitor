using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MarkeDitor.Helpers;

public static class AboutDialog
{
    private const string RepoUrl = "https://github.com/Bat51/MarkeDitor";
    private const string ReleasesUrl = "https://github.com/Bat51/MarkeDitor/releases";
    private const string IssuesUrl = "https://github.com/Bat51/MarkeDitor/issues";
    private const string LicenseUrl = "https://github.com/Bat51/MarkeDitor/blob/main/LICENSE";
    private const string ContactEmail = "teosec@gmail.com";
    private const string LatestReleaseApi = "https://api.github.com/repos/Bat51/MarkeDitor/releases/latest";

    public static Task ShowAsync(Window owner)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        var dialog = new Window
        {
            Title = "About MarkeDitor",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(0x2d, 0x33, 0x3b))
        };

        var fg = new SolidColorBrush(Color.FromRgb(0xcd, 0xd9, 0xe5));
        var muted = new SolidColorBrush(Color.FromRgb(0x76, 0x83, 0x90));
        var link = new SolidColorBrush(Color.FromRgb(0x58, 0xa6, 0xff));

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
            Margin = new Thickness(0, 4, 0, 4)
        };

        var updateStatus = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };
        updateStatus.Children.Add(new TextBlock
        {
            Text = "Checking for updates…",
            FontSize = 11,
            Foreground = muted
        });

        var desc = new TextBlock
        {
            Text = "Cross-platform Markdown editor with live preview.",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Foreground = fg,
            Margin = new Thickness(20, 0, 20, 12)
        };

        var copyright = new TextBlock
        {
            Text = "© 2026 Laurent Massy",
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = muted,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var links = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            Margin = new Thickness(0, 0, 0, 14)
        };
        AddLinkRow(links, 0, "Repository:", "github.com/Bat51/MarkeDitor", RepoUrl, fg, link);
        AddLinkRow(links, 1, "Releases:", "Download latest", ReleasesUrl, fg, link);
        AddLinkRow(links, 2, "Issues:", "Report a bug", IssuesUrl, fg, link);
        AddLinkRow(links, 3, "Contact:", ContactEmail, "mailto:" + ContactEmail, fg, link);

        var licenseRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14)
        };
        licenseRow.Children.Add(new TextBlock
        {
            Text = "Released under the ",
            Foreground = muted
        });
        licenseRow.Children.Add(MakeLink("MIT License", LicenseUrl, link));
        licenseRow.Children.Add(new TextBlock { Text = ".", Foreground = muted });

        var credits = new TextBlock
        {
            Text = "Built with Avalonia, AvaloniaEdit, Markdig,\n" +
                   "Markdown.Avalonia and WeCantSpell.Hunspell.",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            FontSize = 11,
            Foreground = muted,
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
            Children = { icon, name, ver, updateStatus, desc, copyright, links, licenseRow, credits, ok }
        };
        dialog.Content = panel;

        dialog.Opened += async (_, _) =>
        {
            var tag = await FetchLatestVersionTagAsync();
            updateStatus.Children.Clear();

            var current = Assembly.GetExecutingAssembly().GetName().Version;
            var latest = tag is null ? null : ParseVersion(tag);

            if (tag is null || latest is null || current is null)
            {
                updateStatus.Children.Add(new TextBlock
                {
                    Text = "Could not check for updates.",
                    FontSize = 11,
                    Foreground = muted
                });
                return;
            }

            if (Compare(latest, current) > 0)
            {
                updateStatus.Children.Add(new TextBlock
                {
                    Text = $"Update available: {tag} — ",
                    FontSize = 11,
                    Foreground = fg
                });
                var dl = MakeLink("Download", ReleasesUrl, link);
                dl.FontSize = 11;
                updateStatus.Children.Add(dl);
            }
            else
            {
                updateStatus.Children.Add(new TextBlock
                {
                    Text = "You're up to date.",
                    FontSize = 11,
                    Foreground = muted
                });
            }
        };

        return dialog.ShowDialog(owner);
    }

    private static async Task<string?> FetchLatestVersionTagAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MarkeDitor-UpdateCheck");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            var json = await http.GetStringAsync(LatestReleaseApi);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static Version? ParseVersion(string tag)
    {
        var s = tag.TrimStart('v', 'V');
        return Version.TryParse(s, out var v) ? v : null;
    }

    // Compare ignoring Revision (Assembly versions are 4-part, release tags usually 3-part).
    private static int Compare(Version a, Version b)
    {
        if (a.Major != b.Major) return a.Major.CompareTo(b.Major);
        if (a.Minor != b.Minor) return a.Minor.CompareTo(b.Minor);
        var ab = Math.Max(a.Build, 0);
        var bb = Math.Max(b.Build, 0);
        return ab.CompareTo(bb);
    }

    private static void AddLinkRow(Grid grid, int row, string label, string text, string url, IBrush labelBrush, IBrush linkBrush)
    {
        var lbl = new TextBlock
        {
            Text = label,
            Foreground = labelBrush,
            Margin = new Thickness(0, 1, 12, 1),
            MinWidth = 80
        };
        Grid.SetRow(lbl, row);
        Grid.SetColumn(lbl, 0);
        grid.Children.Add(lbl);

        var lnk = MakeLink(text, url, linkBrush);
        lnk.Margin = new Thickness(0, 1, 0, 1);
        Grid.SetRow(lnk, row);
        Grid.SetColumn(lnk, 1);
        grid.Children.Add(lnk);
    }

    private static TextBlock MakeLink(string text, string url, IBrush brush)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = brush,
            TextDecorations = TextDecorations.Underline,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        tb.PointerPressed += (_, _) => OpenUrl(url);
        return tb;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { /* nothing useful to do if the OS can't open the URL */ }
    }
}
