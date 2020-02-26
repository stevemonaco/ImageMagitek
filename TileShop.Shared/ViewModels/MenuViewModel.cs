using System;
using Stylet;
using TileShop.Shared.EventModels;

namespace TileShop.Shared.ViewModels
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

        public void ShowWindow(ToolWindow toolWindow) => _events.PublishOnUIThread(new ShowToolWindowEvent(toolWindow));

        public void ExitApplication()
        {
            //await TryCloseAsync();
            Environment.Exit(0);
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
