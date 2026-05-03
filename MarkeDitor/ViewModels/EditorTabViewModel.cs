using CommunityToolkit.Mvvm.ComponentModel;

namespace MarkeDitor.ViewModels;

public partial class EditorTabViewModel : ObservableObject
{
    public string RecoveryId { get; set; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _fileName = "Untitled";

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private bool _isDirty;

    public string DisplayName => IsDirty ? $"{FileName} *" : FileName;
}
