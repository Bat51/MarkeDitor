using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using MarkeDitor.Models;
using MarkeDitor.ViewModels;

namespace MarkeDitor.Views;

public partial class FileExplorerView : UserControl
{
    private readonly FileExplorerViewModel _viewModel = new();

    public event EventHandler<string>? FileActivated;

    public FileExplorerView()
    {
        DataContext = _viewModel;
        InitializeComponent();
    }

    public void LoadFolder(string folderPath)
    {
        _viewModel.LoadFolder(folderPath);
        var header = this.FindControl<TextBlock>("FolderHeader");
        if (header != null) header.Text = Path.GetFileName(folderPath);
    }

    private void OnFileTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TreeView tree && tree.SelectedItem is FileItem item && !item.IsFolder)
        {
            FileActivated?.Invoke(this, item.FullPath);
        }
    }
}
