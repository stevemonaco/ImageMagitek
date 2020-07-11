using ModernWpf;
using Stylet;
using System.Windows.Threading;
using TileShop.Shared.EventModels;
using TileShop.WPF.EventModels;

namespace TileShop.WPF.ViewModels
{
    public class MenuViewModel : Screen, IHandle<ProjectLoadedEvent>, IHandle<ProjectUnloadedEvent>
    {
        private IEventAggregator _events;

        public MenuViewModel(IEventAggregator events)
        {
            _events = events;
            _events.Subscribe(this);
        }

        private bool _hasProject;
        public bool HasProject
        {
            get => _hasProject;
            set => SetAndNotify(ref _hasProject, value);
        }

        public void NewProject() => _events.PublishOnUIThread(new NewProjectEvent());

        public void OpenProject() => _events.PublishOnUIThread(new OpenProjectEvent());

        public void CloseProject() => _events.PublishOnUIThread(new CloseProjectEvent());

        public void SaveProject() => _events.PublishOnUIThread(new SaveProjectEvent(false));

        public void SaveProjectAs() => _events.PublishOnUIThread(new SaveProjectEvent(true));

        public void AddDataFile() => _events.PublishOnUIThread(new AddDataFileEvent());

        public void AddPalette() => _events.PublishOnUIThread(new AddPaletteEvent());

        public void AddScatteredArranger() => _events.PublishOnUIThread(new AddScatteredArrangerEvent());

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

        public void Handle(ProjectLoadedEvent message)
        {
            HasProject = true;
        }
        public void Handle(ProjectUnloadedEvent message)
        {
            HasProject = false;
        }
    }
}
