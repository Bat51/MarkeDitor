using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MarkeDitor.Helpers;

namespace MarkeDitor.Views;

public partial class ToolbarView : UserControl
{
    public Func<EditorBridge?>? EditorProvider { get; set; }

    public ToolbarView()
    {
        InitializeComponent();
    }

    private void OnBold(object? sender, RoutedEventArgs e)
        => EditorProvider?.Invoke()?.WrapSelection("**", "**");

    private void OnItalic(object? sender, RoutedEventArgs e)
        => EditorProvider?.Invoke()?.WrapSelection("*", "*");

    private void OnH1(object? sender, RoutedEventArgs e)
        => EditorProvider?.Invoke()?.InsertAtLineStart("# ");

    private void OnH2(object? sender, RoutedEventArgs e)
        => EditorProvider?.Invoke()?.InsertAtLineStart("## ");

    private void OnH3(object? sender, RoutedEventArgs e)
        => EditorProvider?.Invoke()?.InsertAtLineStart("### ");

    private void OnLink(object? sender, RoutedEventArgs e)
        => EditorProvider?.Invoke()?.WrapSelection("[", "](url)");

    private void OnImage(object? sender, RoutedEventArgs e)
        => EditorProvider?.Invoke()?.WrapSelection("![", "](url)");

    private void OnCode(object? sender, RoutedEventArgs e)
        => EditorProvider?.Invoke()?.WrapSelection("```\n", "\n```");

    private void OnBulletList(object? sender, RoutedEventArgs e)
        => EditorProvider?.Invoke()?.InsertAtLineStart("- ");

    private void OnNumberList(object? sender, RoutedEventArgs e)
        => EditorProvider?.Invoke()?.InsertAtLineStart("1. ");

    private void OnQuote(object? sender, RoutedEventArgs e)
        => EditorProvider?.Invoke()?.InsertAtLineStart("> ");
}
