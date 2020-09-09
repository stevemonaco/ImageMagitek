using ModernWpf;
using Stylet;
using System;
using System.Windows.Threading;
using TileShop.Shared.EventModels;
using TileShop.WPF.EventModels;

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

        //public void AddDataFile() => _events.PublishOnUIThread(new AddDataFileEvent());
        //public void AddPalette() => _events.PublishOnUIThread(new AddPaletteEvent());
        //public void AddScatteredArranger() => _events.PublishOnUIThread(new AddScatteredArrangerEvent());

        public void ShowWindow(ToolWindow toolWindow) => _events.PublishOnUIThread(new ShowToolWindowEvent(toolWindow));

        public void ExitApplication() => _events.PublishOnUIThread(new RequestApplicationExitEvent());

        public void ToggleTheme()
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                if (ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark)
                    ThemeManager.Current.SetCurrentValue(ThemeManager.ApplicationThemeProperty, ApplicationTheme.Light);
                else if (ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Light)
                    ThemeManager.Current.SetCurrentValue(ThemeManager.ApplicationThemeProperty, ApplicationTheme.Dark);
            });
        }
    }
}
