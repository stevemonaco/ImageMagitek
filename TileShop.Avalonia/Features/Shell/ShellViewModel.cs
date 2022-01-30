using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek;
using ImageMagitek.Project;
using ImageMagitek.Services;
using Jot;
using TileShop.AvaloniaUI.Imaging;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly Tracker _tracker;
    private readonly IProjectService _projectService;
    private string _projectFile = @"D:\ImageMagitekTest\FF2\FF2project.xml";
    private ProjectTree _projectTree;

    [ObservableProperty] private BitmapAdapter? _bitmapAdapter;
    [ObservableProperty] private ProjectTreeViewModel _activeTree;
    [ObservableProperty] private MenuViewModel _activeMenu;
    [ObservableProperty] private EditorsViewModel _editors;

    public ShellViewModel(Tracker tracker, IProjectService projectService, ProjectTreeViewModel activeTree,
        MenuViewModel activeMenu, EditorsViewModel editors)
    {
        _tracker = tracker;
        _projectService = projectService;
        _activeTree = activeTree;
        _activeMenu = activeMenu;
        _editors = editors;
    }

    [ICommand]
    public void Load()
    {
        ActiveTree.OpenProject(_projectFile);
    }

    public void RequestApplicationExit()
    {
        if (Editors.RequestSaveAllUserChanges())
        {
            _projectService.CloseProjects();
            _tracker.PersistAll();
            Environment.Exit(0);
        }
    }
}
