using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MarkeDitor.Helpers;

public enum DialogResult { None, Primary, Secondary, Cancel }

public static class DialogHelper
{
    public static Task<DialogResult> ShowYesNoCancelAsync(Window owner, string title, string content,
        string primaryText, string secondaryText, string cancelText)
        => ShowAsync(owner, title, content, primaryText, secondaryText, cancelText);

    public static Task<DialogResult> ShowYesNoAsync(Window owner, string title, string content,
        string primaryText, string secondaryText)
        => ShowAsync(owner, title, content, primaryText, secondaryText, null);

    private static async Task<DialogResult> ShowAsync(Window owner, string title, string content,
        string primaryText, string? secondaryText, string? cancelText)
    {
        var result = DialogResult.None;
        var dialog = new Window
        {
            Title = title,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        dialog.BindToResource(Window.BackgroundProperty, "AppPanelBrush");

        var contentBlock = new TextBlock
        {
            Text = content,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20, 16)
        };
        contentBlock.BindToResource(TextBlock.ForegroundProperty, "AppForegroundBrush");

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12),
            Spacing = 8
        };

        var primary = new Button
        {
            Content = primaryText,
            MinWidth = 120,
            IsDefault = true
        };
        primary.Click += (_, _) => { result = DialogResult.Primary; dialog.Close(); };
        buttons.Children.Add(primary);

        if (!string.IsNullOrEmpty(secondaryText))
        {
            var secondary = new Button { Content = secondaryText, MinWidth = 120 };
            secondary.Click += (_, _) => { result = DialogResult.Secondary; dialog.Close(); };
            buttons.Children.Add(secondary);
        }

        if (!string.IsNullOrEmpty(cancelText))
        {
            var cancel = new Button { Content = cancelText, MinWidth = 90, IsCancel = true };
            cancel.Click += (_, _) => { result = DialogResult.Cancel; dialog.Close(); };
            buttons.Children.Add(cancel);
        }

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        Grid.SetRow(contentBlock, 0);
        Grid.SetRow(buttons, 1);
        root.Children.Add(contentBlock);
        root.Children.Add(buttons);
        dialog.Content = root;

        await dialog.ShowDialog(owner);
        return result;
    }
}
