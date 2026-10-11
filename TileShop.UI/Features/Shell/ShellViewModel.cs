using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Services;
using TileShop.Shared.Services;

namespace TileShop.UI.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly UserPreferencesStore _preferencesStore;
    private readonly IProjectService _projectService;

    [ObservableProperty] private ProjectTreeViewModel _activeTree;
    [ObservableProperty] private MenuViewModel _activeMenu;
    [ObservableProperty] private StatusViewModel _activeStatusBar;
    [ObservableProperty] private EditorsViewModel _editors;

    public WelcomeViewModel Welcome { get; }
    public ExitGate ExitGate { get; } = new();

    public ShellViewModel(UserPreferencesStore preferencesStore, IProjectService projectService, ProjectTreeViewModel activeTree,
        MenuViewModel activeMenu, StatusViewModel activeStatusBar, EditorsViewModel editors, WelcomeViewModel welcome,
        INotificationService notifications, BootstrapService bootstrapService)
    {
        _preferencesStore = preferencesStore;
        _projectService = projectService;
        _activeTree = activeTree;
        _activeMenu = activeMenu;
        _activeStatusBar = activeStatusBar;
        _editors = editors;
        Welcome = welcome;

        _editors.Shell = this;
        _activeMenu.Shell = this;

        StartupIssueNotifier.Post(notifications, bootstrapService.Issues);
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
