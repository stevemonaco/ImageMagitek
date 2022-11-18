using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek.Services;
using Jot;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly Tracker _tracker;
    private readonly IProjectService _projectService;
    private string _projectFile = @"D:\ImageMagitekTest\FF2\FF2project.xml";

    [ObservableProperty] private ProjectTreeViewModel _activeTree;
    [ObservableProperty] private MenuViewModel _activeMenu;
    [ObservableProperty] private StatusViewModel _activeStatusBar;
    [ObservableProperty] private EditorsViewModel _editors;

    public ShellViewModel(Tracker tracker, IProjectService projectService, ProjectTreeViewModel activeTree,
        MenuViewModel activeMenu, StatusViewModel activeStatusBar, EditorsViewModel editors)
    {
        _tracker = tracker;
        _projectService = projectService;
        _activeTree = activeTree;
        _activeMenu = activeMenu;
        _activeStatusBar = activeStatusBar;
        _editors = editors;

        _editors.Shell = this;
        _activeMenu.Shell = this;
    }

    [RelayCommand]
    public async Task Load()
    {
        await ActiveTree.OpenProject(_projectFile);
    }

    public async Task<bool> PrepareApplicationExit()
    {
        var canClose = await Editors.RequestSaveAllUserChanges();

        if (canClose)
        {
            _projectService.CloseProjects();
            _tracker.PersistAll();
        }

        return canClose;
    }
}
