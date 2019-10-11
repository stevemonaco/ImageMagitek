using System;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using TileShop.Shared.EventModels;

namespace TileShop.WPF.ViewModels
{
    public class MenuViewModel : Screen, IHandle<ProjectLoadedEvent>, IHandle<ProjectUnloadedEvent>
    {
        private IEventAggregator _events;

        public MenuViewModel(IEventAggregator events)
        {
            _events = events;
            _events.SubscribeOnUIThread(this);
        }

        private bool _hasProject;
        public bool HasProject
        {
            get => _hasProject;
            set => Set(ref _hasProject, value);
        }

        public Task NewProject() => _events.PublishOnUIThreadAsync(new NewProjectEvent());

        public Task OpenProject() => _events.PublishOnUIThreadAsync(new OpenProjectEvent());

        public Task CloseProject() => _events.PublishOnUIThreadAsync(new CloseProjectEvent());

        public Task SaveProject() => _events.PublishOnUIThreadAsync(new SaveProjectEvent(false));

        public Task SaveProjectAs() => _events.PublishOnUIThreadAsync(new SaveProjectEvent(true));

        public Task AddDataFile() => _events.PublishOnUIThreadAsync(new AddDataFileEvent());

        public Task AddPalette() => _events.PublishOnUIThreadAsync(new AddPaletteEvent());

        public void ExitApplication()
        {
            //await TryCloseAsync();
            Environment.Exit(0);
        }

        public Task HandleAsync(ProjectLoadedEvent message, CancellationToken cancellationToken)
        {
            HasProject = true;
            return Task.CompletedTask;
        }

        public Task HandleAsync(ProjectUnloadedEvent message, CancellationToken cancellationToken)
        {
            HasProject = false;
            return Task.CompletedTask;
        }
    }
}
