using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MarkeDitor.Models;

namespace MarkeDitor.ViewModels;

public partial class FileExplorerViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _rootPath;

    public ObservableCollection<FileItem> Items { get; } = new();

    public void LoadFolder(string folderPath)
    {
        RootPath = folderPath;
        Items.Clear();
        LoadDirectory(folderPath, Items);
    }

    private void LoadDirectory(string path, ObservableCollection<FileItem> target)
    {
        try
        {
            // Add directories first
            foreach (var dir in Directory.GetDirectories(path).OrderBy(d => Path.GetFileName(d)))
            {
                var dirItem = new FileItem
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    IsFolder = true
                };
                LoadDirectory(dir, dirItem.Children);
                // Only add if it contains .md files (directly or in subdirectories)
                if (dirItem.Children.Count > 0)
                {
                    target.Add(dirItem);
                }
            }

            // Add .md files
            foreach (var file in Directory.GetFiles(path, "*.md").OrderBy(f => Path.GetFileName(f)))
            {
                target.Add(new FileItem
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    IsFolder = false
                });
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }
    }
}
