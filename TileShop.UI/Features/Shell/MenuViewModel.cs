using System.IO;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using TileShop.Shared.Services;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using TileShop.Shared.Interactions;
using System;
using System.Diagnostics;
using System.Reflection;
using TileShop.UI.Features.Graphics;
using TileShop.UI.Models;

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
                _themeService.SetActiveTheme(value);
                _preferencesStore.Preferences.Theme = value;
                _preferencesStore.Save();
            }
        }
    }

    private readonly UserPreferencesStore _preferencesStore;
    private readonly IThemeService _themeService;
    private readonly IInteractionService _interactions;
    private readonly IExploreService _exploreService;
    private readonly IPluginService _pluginService;
    private readonly AppSettings _settings;

    public MenuViewModel(UserPreferencesStore preferencesStore, IThemeService themeService, ProjectTreeViewModel projectTreeVm, EditorsViewModel editors,
        IInteractionService interactionService, IExploreService exploreService, IProjectService projectService, IPluginService pluginService,
        AppSettings settings)
    {
        _pluginService = pluginService;
        _settings = settings;
        _preferencesStore = preferencesStore;
        _themeService = themeService;
        _projectTree = projectTreeVm;
        _editors = editors;
        _interactions = interactionService;
        _exploreService = exploreService;

        projectService.ProjectOpened += (_, tree) =>
        {
            if (!tree.IsStandaloneFile && tree.Root.DiskLocation is { } projectFileName)
                AddRecentProjectFile(projectFileName);
        };

        var preferences = preferencesStore.Preferences;
        _recentProjectFiles = new(preferences.RecentProjectFiles.Where(File.Exists));
        _activeTheme = preferences.Theme;
        _themeService.SetActiveTheme(_activeTheme);
    }

    [RelayCommand]
    public async Task NewEmptyProject() => await ProjectTree.AddNewProject();

    [RelayCommand]
    public async Task NewProjectFromFile() => await ProjectTree.NewProjectFromFile();

    [RelayCommand]
    public async Task OpenDataFile() => await ProjectTree.OpenDataFile();

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
    public async Task ExportArrangerToImage(GraphicsEditorViewModel vm) =>
        await ProjectTree.ExportArrangerAs((ScatteredArranger) vm.Resource);

    [RelayCommand]
    public async Task ImportArrangerFromImage(GraphicsEditorViewModel vm) =>
        await ProjectTree.ImportArrangerFrom((ScatteredArranger) vm.Resource);

    [RelayCommand]
    public async Task OpenAbout()
    {
        var version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;

        var plugins = _pluginService.CodecPlugins.Count > 0
            ? "Plugin codecs:\n" + string.Join("\n", _pluginService.CodecPlugins.Select(x => x.Name))
            : "No plugin codecs loaded";

        var heading = "TileShop";
        var message = $"Version: {version}\n\n{plugins}";
        await _interactions.AlertAsync(heading, message);
    }

    [RelayCommand]
    public async Task OpenPreferences()
    {
        var preferences = _preferencesStore.Preferences;
        var model = new PreferencesViewModel(preferences, preferences.NesPalette ?? _settings.NesPalette);

        if (await _interactions.RequestAsync(model) is not { } result)
            return;

        ActiveTheme = result.Theme;
        preferences.EnableArrangerSymmetryTools = result.EnableArrangerSymmetryTools;
        preferences.JumpToOffsetBase = result.JumpToOffsetBase;
        preferences.Grid = new(GridSettingsViewModel.ToHex(result.LineColor), GridSettingsViewModel.ToHex(result.PrimaryColor),
            GridSettingsViewModel.ToHex(result.SecondaryColor));
        preferences.NesPalette = result.NesPalette == _settings.NesPalette ? null : result.NesPalette;
        _preferencesStore.Save();
    }

    [RelayCommand]
    public void OpenWiki()
    {
        var uri = new Uri("https://github.com/stevemonaco/ImageMagitek/wiki");
        _exploreService.ExploreWebLocation(uri);
    }

    private async void AddRecentProjectFile(string projectFileName)
    {
        await Task.Yield(); // Delay so that the menu closes, otherwise changing the collection keeps it open

        if (RecentProjectFiles.Contains(projectFileName))
        {
            RecentProjectFiles.Remove(projectFileName);
            RecentProjectFiles.Insert(0, projectFileName);
        }
        else
        {
            RecentProjectFiles.Insert(0, projectFileName);
            if (RecentProjectFiles.Count > 8)
                RecentProjectFiles = new(RecentProjectFiles.Take(8));
        }

        _preferencesStore.Preferences.RecentProjectFiles = [.. RecentProjectFiles];
        _preferencesStore.Save();
    }
}
