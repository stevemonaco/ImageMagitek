using System.IO;
using System.Linq;
using Jot;
using ImageMagitek;
using TileShop.Shared.EventModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using TileShop.Shared.Services;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm;
using TileShop.Shared.Interactions;
using System;
using System.Diagnostics;
using System.Reflection;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class MenuViewModel : ObservableRecipient
{
    [ObservableProperty] private ShellViewModel _shell;
    [ObservableProperty] private ProjectTreeViewModel _projectTree;
    [ObservableProperty] private EditorsViewModel _editors;
    [ObservableProperty] private ObservableCollection<string> _recentProjectFiles = new();

    private ThemeStyle _activeTheme;
    public ThemeStyle ActiveTheme
    {
        get => _activeTheme;
        set
        {
            if (SetProperty(ref _activeTheme, value))
            {
                _themeService.SetActiveTheme(ActiveTheme);
            }
        }
    }

    private readonly Tracker _tracker;
    private readonly IThemeService _themeService;
    private readonly IInteractionService _interactions;
    private readonly IExploreService _exploreService;

    public MenuViewModel(Tracker tracker, IThemeService themeService, ProjectTreeViewModel projectTreeVM, EditorsViewModel editors,
        IInteractionService interactionService, IExploreService exploreService)
    {
        _tracker = tracker;
        _themeService = themeService;
        _projectTree = projectTreeVM;
        _editors = editors;
        _interactions = interactionService;
        _exploreService = exploreService;
        _tracker.Track(this);
        Messenger.Register<ProjectLoadedEvent>(this, (r, m) => Handle(m));

        RecentProjectFiles = new(RecentProjectFiles.Where(x => File.Exists(x)));
        ActiveTheme = _themeService.ActiveTheme;
    }

    [RelayCommand]
    public async Task NewEmptyProject() => await ProjectTree.AddNewProject();

    [RelayCommand]
    public async Task NewProjectFromFile() => await ProjectTree.NewProjectFromFile();

    [RelayCommand]
    public async Task OpenProject() => await ProjectTree.OpenProject();

    [RelayCommand]
    public async Task OpenRecentProject(string projectFileName) => await ProjectTree.OpenProject(projectFileName);

    [RelayCommand]
    public async Task CloseAllProjects() => await ProjectTree.CloseAllProjects();

    [RelayCommand]
    public async Task CloseEditor() => await Editors.CloseEditor(Editors.ActiveEditor);

    [RelayCommand]
    public async Task SaveEditor() => await Editors.ActiveEditor?.SaveChangesAsync();


    [RelayCommand]
    public void ChangeToLightTheme()
    {
        ActiveTheme = ThemeStyle.Light;
    }

    [RelayCommand]
    public void ChangeToDarkTheme()
    {
        ActiveTheme = ThemeStyle.Dark;
    }

    [RelayCommand]
    public async Task ExportArrangerToImage(ScatteredArrangerEditorViewModel vm) =>
        await ProjectTree.ExportArrangerAs((ScatteredArranger) vm.Resource);

    [RelayCommand]
    public async Task ImportArrangerFromImage(ScatteredArrangerEditorViewModel vm) =>
        await ProjectTree.ImportArrangerFrom((ScatteredArranger) vm.Resource);

    [RelayCommand]
    public async Task OpenAbout()
    {
        var version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;

        var heading = "TileShop";
        var message = $"Version: {version}";
        await _interactions.AlertAsync(heading, message);
    }

    [RelayCommand]
    public void OpenWiki()
    {
        var uri = new Uri("https://github.com/stevemonaco/ImageMagitek/wiki");
        _exploreService.ExploreWebLocation(uri);
    }

    public async void Handle(ProjectLoadedEvent message)
    {
        await Task.Delay(50); // Delay so that the menu closes, otherwise changing the collection keeps it open

        if (RecentProjectFiles.Contains(message.ProjectFileName))
        {
            RecentProjectFiles.Remove(message.ProjectFileName);
            RecentProjectFiles.Insert(0, message.ProjectFileName);
        }
        else
        {
            RecentProjectFiles.Insert(0, message.ProjectFileName);
            if (RecentProjectFiles.Count > 8)
                RecentProjectFiles = new(RecentProjectFiles.Take(8));
        }
    }
}
