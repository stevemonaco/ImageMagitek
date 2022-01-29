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
    private string _arrangerKey = @"/Character Overworld Sprites/Cecil Map";
    private ProjectTree _projectTree;

    [ObservableProperty] private string _message = "Testing 123";
    [ObservableProperty] private BitmapAdapter _bitmapAdapter;
    [ObservableProperty] private ObservableCollection<ProjectNodeViewModel> _projects = new();

    public ShellViewModel(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [ICommand]
    public void Load()
    {
        var result = _projectService.OpenProjectFile(_projectFile);

        result.Switch(success =>
        {
            _projectTree = success.Result;
            var projectVM = new ProjectNodeViewModel((ProjectNode)success.Result.Root);
            _projects.Add(projectVM);

            _projectTree.TryGetItem<ScatteredArranger>(_arrangerKey, out var arranger);
            var image = new IndexedImage(arranger);
            BitmapAdapter = new IndexedBitmapAdapter(image);
        },
        failed =>
        {

        });
    }

    public void Test()
    {

    }
}
