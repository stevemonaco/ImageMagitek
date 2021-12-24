using ImageMagitek;
using Jot;
using ModernWpf;
using Stylet;
using System.IO;
using System.Linq;
using TileShop.Shared.EventModels;

namespace TileShop.WPF.ViewModels;

public class MenuViewModel : Screen, IHandle<ProjectLoadedEvent>
{
    private ShellViewModel _shell;
    public ShellViewModel Shell
    {
        get => _shell;
        set => SetAndNotify(ref _shell, value);
    }

    private ProjectTreeViewModel _projectTree;
    public ProjectTreeViewModel ProjectTree
    {
        get => _projectTree;
        set => SetAndNotify(ref _projectTree, value);
    }

    private EditorsViewModel _editors;
    public EditorsViewModel Editors
    {
        get => _editors;
        set => SetAndNotify(ref _editors, value);
    }

    private BindableCollection<string> _recentProjectFiles = new();
    public BindableCollection<string> RecentProjectFiles
    {
        get => _recentProjectFiles;
        set => SetAndNotify(ref _recentProjectFiles, value);
    }

    private readonly IEventAggregator _events;
    private readonly Tracker _tracker;

    public MenuViewModel(IEventAggregator events, ProjectTreeViewModel projectTreeVM, EditorsViewModel editors, Tracker tracker)
    {
        _events = events;
        ProjectTree = projectTreeVM;
        Editors = editors;
        _tracker = tracker;

        _events.Subscribe(this);
        _tracker.Track(this);

        RecentProjectFiles = new(RecentProjectFiles.Where(x => File.Exists(x)));
    }

    public void NewEmptyProject() => ProjectTree.AddNewProject();

    public void NewProjectFromFile() => ProjectTree.NewProjectFromFile();

    public void OpenProject() => ProjectTree.OpenProject();

    public void OpenRecentProject(string projectFileName) => ProjectTree.OpenProject(projectFileName);

    public void CloseAllProjects() => ProjectTree.CloseAllProjects();

    public void CloseEditor() => Editors.CloseEditor(Editors.ActiveEditor);

    public void SaveEditor() => Editors.ActiveEditor?.SaveChanges();

    public void ShowWindow(ToolWindow toolWindow) => _events.PublishOnUIThread(new ShowToolWindowEvent(toolWindow));

    public void ExitApplication() => Shell.RequestApplicationExit();

    public void ChangeToLightTheme() => Shell.Theme = ApplicationTheme.Light;

    public void ChangeToDarkTheme() => Shell.Theme = ApplicationTheme.Dark;

    public void ExportArrangerToImage(ScatteredArrangerEditorViewModel vm) =>
        ProjectTree.ExportArrangerAs(vm.Resource as ScatteredArranger);

    public void ImportArrangerFromImage(ScatteredArrangerEditorViewModel vm) =>
        ProjectTree.ImportArrangerFrom(vm.Resource as ScatteredArranger);

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
