using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MarkeDitor.Services;

namespace MarkeDitor.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly FileService _fileService;
    private readonly DialogService _dialogService;

    [ObservableProperty]
    private EditorTabViewModel? _activeTab;

    public ObservableCollection<EditorTabViewModel> Tabs { get; } = new();

    public MainViewModel(FileService fileService, DialogService dialogService)
    {
        _fileService = fileService;
        _dialogService = dialogService;
    }
}
