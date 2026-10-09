using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek.Services;
using TileShop.Shared.Interactions;
using TileShop.Shared.Services;

namespace TileShop.UI.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly UserPreferencesStore _preferencesStore;
    private readonly IProjectService _projectService;
#if DEBUG
    private const string _debugProjectFile = @"D:\ImageMagitekTest\FF2\FF2project.xml";
#endif

    [ObservableProperty] private ProjectTreeViewModel _activeTree;
    [ObservableProperty] private MenuViewModel _activeMenu;
    [ObservableProperty] private StatusViewModel _activeStatusBar;
    [ObservableProperty] private EditorsViewModel _editors;
    private readonly IInteractionService _interactionService;
    private readonly BootstrapService _bootstrapService;

    public ExitGate ExitGate { get; } = new();

    public ShellViewModel(UserPreferencesStore preferencesStore, IProjectService projectService, ProjectTreeViewModel activeTree,
        MenuViewModel activeMenu, StatusViewModel activeStatusBar, EditorsViewModel editors, IInteractionService interactionService,
        BootstrapService bootstrapService)
    {
        _bootstrapService = bootstrapService;
        _preferencesStore = preferencesStore;
        _projectService = projectService;
        _activeTree = activeTree;
        _activeMenu = activeMenu;
        _activeStatusBar = activeStatusBar;
        _editors = editors;
        _interactionService = interactionService;

        _editors.Shell = this;
        _activeMenu.Shell = this;
    }

#if DEBUG
    [RelayCommand]
    public async Task DebugLoad()
    {
        await ActiveTree.OpenProject(_debugProjectFile);
    }
#endif

    /// <summary>
    /// Alerts the user once to the resource files that startup skipped, if any
    /// </summary>
    public async Task ShowStartupIssuesAsync()
    {
        var issues = _bootstrapService.Issues;
        if (issues.Count == 0)
            return;

        await _interactionService.AlertAsync("Some resources were not loaded",
            StartupIssueFormatter.Format(issues, TileShopBootstrapper.LogDirectory));
    }

    public async Task<bool> PrepareApplicationExit()
    {
        var canClose = await Editors.RequestSaveAllUserChanges();

        if (canClose)
        {
            _projectService.CloseProjects();
            _preferencesStore.Save();
        }

        return canClose;
    }
}
