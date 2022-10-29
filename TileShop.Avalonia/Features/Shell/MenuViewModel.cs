using System.IO;
using System.Linq;
using Jot;
using ImageMagitek;
using TileShop.Shared.EventModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using TileShop.Shared.Services;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class MenuViewModel : ObservableRecipient
{
    [ObservableProperty] private ShellViewModel _shell;
    [ObservableProperty] private ProjectTreeViewModel _projectTree;
    [ObservableProperty] private EditorsViewModel _editors;
    [ObservableProperty] private ObservableCollection<string> _recentProjectFiles = new();

    private readonly Tracker _tracker;
    private readonly IThemeService _themeService;

    public MenuViewModel(Tracker tracker, IThemeService themeService, ProjectTreeViewModel projectTreeVM, EditorsViewModel editors)
    {
        _tracker = tracker;
        _themeService = themeService;
        ProjectTree = projectTreeVM;
        Editors = editors;

        _tracker.Track(this);
        Messenger.Register<ProjectLoadedEvent>(this, (r, m) => Handle(m));

        RecentProjectFiles = new(RecentProjectFiles.Where(x => File.Exists(x)));
    }

    public void NewEmptyProject() => ProjectTree.AddNewProject();

    public void NewProjectFromFile() => ProjectTree.NewProjectFromFile();

    public void OpenProject() => ProjectTree.OpenProject();

    public void OpenRecentProject(string projectFileName) => ProjectTree.OpenProject(projectFileName);

    public void CloseAllProjects() => ProjectTree.CloseAllProjects();

    public void CloseEditor() => Editors.CloseEditor(Editors.ActiveEditor);

    public void SaveEditor() => Editors.ActiveEditor?.SaveChangesAsync();

    public void ExitApplication() => Shell.RequestApplicationExit();

    public void ChangeToLightTheme() => _themeService.SetActiveTheme("Fluent Light");

    public void ChangeToDarkTheme() => _themeService.SetActiveTheme("Fluent Dark");

    public void ExportArrangerToImage(ScatteredArrangerEditorViewModel vm) =>
        ProjectTree.ExportArrangerAs((ScatteredArranger) vm.Resource);

    public void ImportArrangerFromImage(ScatteredArrangerEditorViewModel vm) =>
        ProjectTree.ImportArrangerFrom((ScatteredArranger) vm.Resource);

    public void Handle(ProjectLoadedEvent message)
    {
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
