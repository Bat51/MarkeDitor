using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MarkeDitor.Services;

public class DialogService
{
    private readonly Window _window;

    public DialogService(Window window)
    {
        _window = window;
    }

    public async Task<string?> ShowOpenFileDialogAsync()
    {
        var storage = TopLevel.GetTopLevel(_window)?.StorageProvider;
        if (storage == null) return null;

        var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Markdown") { Patterns = new[] { "*.md", "*.markdown" } },
                new("Text") { Patterns = new[] { "*.txt" } },
                new("All files") { Patterns = new[] { "*" } },
            }
        });

        if (result.Count == 0) return null;
        return result[0].TryGetLocalPath();
    }

    public async Task<string?> ShowOpenFolderDialogAsync()
    {
        var storage = TopLevel.GetTopLevel(_window)?.StorageProvider;
        if (storage == null) return null;

        var result = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        if (result.Count == 0) return null;
        return result[0].TryGetLocalPath();
    }

    public async Task<string?> ShowSaveFileDialogAsync(string suggestedFileName)
    {
        var storage = TopLevel.GetTopLevel(_window)?.StorageProvider;
        if (storage == null) return null;

        var result = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "md",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("Markdown") { Patterns = new[] { "*.md", "*.markdown" } },
                new("Text") { Patterns = new[] { "*.txt" } },
            }
        });

        return result?.TryGetLocalPath();
    }

    public async Task<string?> ShowSaveHtmlDialogAsync(string suggestedFileName)
    {
        var storage = TopLevel.GetTopLevel(_window)?.StorageProvider;
        if (storage == null) return null;

        var result = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "html",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("HTML") { Patterns = new[] { "*.html", "*.htm" } },
            }
        });

        return result?.TryGetLocalPath();
    }
}
