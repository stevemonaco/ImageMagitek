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
    private string _projectFile = @"D:\ImageMagitekTest\FF2\FF2project.xml";

    [ObservableProperty] private ProjectTreeViewModel _activeTree;
    [ObservableProperty] private MenuViewModel _activeMenu;
    [ObservableProperty] private StatusViewModel _activeStatusBar;
    [ObservableProperty] private EditorsViewModel _editors;
    private readonly IInteractionService _interactionService;

    public ShellViewModel(UserPreferencesStore preferencesStore, IProjectService projectService, ProjectTreeViewModel activeTree,
        MenuViewModel activeMenu, StatusViewModel activeStatusBar, EditorsViewModel editors, IInteractionService interactionService)
    {
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

    [RelayCommand]
    public async Task DebugLoad()
    {
        await ActiveTree.OpenProject(_projectFile);
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
