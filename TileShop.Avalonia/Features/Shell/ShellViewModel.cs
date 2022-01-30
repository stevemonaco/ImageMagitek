using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek;
using ImageMagitek.Project;
using ImageMagitek.Services;
using TileShop.AvaloniaUI.Imaging;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private string _projectFile = @"D:\ImageMagitekTest\FF2\FF2project.xml";
    private ProjectTree _projectTree;

    [ObservableProperty] private BitmapAdapter? _bitmapAdapter;
    [ObservableProperty] private ProjectTreeViewModel _activeTree;
    [ObservableProperty] private EditorsViewModel _editors;

    public ShellViewModel(IProjectService projectService, ProjectTreeViewModel activeTree, EditorsViewModel editors)
    {
        _projectService = projectService;
        _activeTree = activeTree;
        _editors = editors;
    }

    [ICommand]
    public void Load()
    {
        ActiveTree.OpenProject(_projectFile);
    }
}
