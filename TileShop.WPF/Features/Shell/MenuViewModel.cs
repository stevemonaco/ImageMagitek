using ModernWpf;
using Stylet;
using TileShop.Shared.EventModels;

namespace TileShop.WPF.ViewModels
{
    public class MenuViewModel : Screen
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

        private readonly IEventAggregator _events;

        public MenuViewModel(IEventAggregator events, ProjectTreeViewModel projectTreeVM, EditorsViewModel editors)
        {
            _events = events;
            ProjectTree = projectTreeVM;
            Editors = editors;
        }

        public void NewProject() => ProjectTree.AddNewProject();

        public void OpenProject() => ProjectTree.OpenProject();

        public void CloseAllProjects() => ProjectTree.CloseAllProjects();

        public void CloseEditor() => Editors.CloseEditor(Editors.ActiveEditor);

        public void SaveEditor() => Editors.ActiveEditor?.SaveChanges();

        public void ShowWindow(ToolWindow toolWindow) => _events.PublishOnUIThread(new ShowToolWindowEvent(toolWindow));

        public void ExitApplication() => _events.PublishOnUIThread(new RequestApplicationExitEvent());

        public void ChangeToLightTheme() => Shell.Theme = ApplicationTheme.Light;

        public void ChangeToDarkTheme() => Shell.Theme = ApplicationTheme.Dark;
    }
}
