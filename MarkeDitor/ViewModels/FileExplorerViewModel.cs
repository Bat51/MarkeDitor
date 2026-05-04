using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MarkeDitor.Models;

namespace MarkeDitor.ViewModels;

public partial class FileExplorerViewModel : ObservableObject
{
    // Caps recursion. Anything deeper than this would be either a typo (user
    // opened "/" by mistake) or a symlink loop (e.g. ~/.wine/dosdevices/z: → /).
    private const int MaxDepth = 8;

    [ObservableProperty]
    private string? _rootPath;

    public ObservableCollection<FileItem> Items { get; } = new();

    public void LoadFolder(string folderPath)
    {
        RootPath = folderPath;
        Items.Clear();
        LoadDirectory(folderPath, Items, depth: 0);
    }

    private void LoadDirectory(string path, ObservableCollection<FileItem> target, int depth)
    {
        if (depth >= MaxDepth)
        {
            return;
        }

        try
        {
            foreach (var dir in Directory.GetDirectories(path).OrderBy(d => Path.GetFileName(d)))
            {
                // Don't follow symlinks — they can produce arbitrarily deep loops
                // (e.g. ~/.wine/dosdevices/z: → /), and they often cross into
                // unstable kernel paths like /dev/fd/<n> that vanish mid-walk.
                if (IsReparsePoint(dir))
                {
                    continue;
                }

                var dirItem = new FileItem
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    IsFolder = true
                };
                LoadDirectory(dir, dirItem.Children, depth + 1);
                if (dirItem.Children.Count > 0)
                {
                    target.Add(dirItem);
                }
            }

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
            // Skip directories we can't access.
        }
        catch (IOException)
        {
            // Skip directories that vanish mid-walk (transient FDs under /proc, /dev/fd, etc.)
            // or that we otherwise can't enumerate.
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }
}
