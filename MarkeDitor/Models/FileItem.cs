using System.Collections.ObjectModel;

namespace MarkeDitor.Models;

public class FileItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public ObservableCollection<FileItem> Children { get; } = new();
}
