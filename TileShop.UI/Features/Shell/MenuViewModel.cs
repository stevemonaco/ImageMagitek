using System.IO;
using System.Linq;
using Jot;
using ImageMagitek;
using TileShop.Shared.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using TileShop.Shared.Services;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using TileShop.Shared.Interactions;
using System;
using System.Diagnostics;
using System.Reflection;

namespace TileShop.UI.ViewModels;

public partial class MenuViewModel : ObservableRecipient
{
    [ObservableProperty] private ShellViewModel _shell = null!;
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

    private readonly IThemeService _themeService;
    private readonly IInteractionService _interactions;
    private readonly IExploreService _exploreService;

    public MenuViewModel(Tracker tracker, IThemeService themeService, ProjectTreeViewModel projectTreeVm, EditorsViewModel editors,
        IInteractionService interactionService, IExploreService exploreService)
    {
        _themeService = themeService;
        _projectTree = projectTreeVm;
        _editors = editors;
        _interactions = interactionService;
        _exploreService = exploreService;
        
        tracker.Track(this);
        Messenger.Register<ProjectLoadedMessage>(this, (r, m) => Handle(m));

        RecentProjectFiles = new(RecentProjectFiles.Where(File.Exists));
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
    public async Task CloseEditor()
    {
        if (Editors.ActiveEditor is not null)
            await Editors.CloseEditor(Editors.ActiveEditor);
    }

    [RelayCommand]
    public async Task SaveEditor()
    {
        if (Editors.ActiveEditor is not null)
            await Editors.ActiveEditor.SaveChangesAsync();
    }


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

    private async void Handle(ProjectLoadedMessage message)
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
