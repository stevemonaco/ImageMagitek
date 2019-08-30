using System;
using System.Threading.Tasks;
using Caliburn.Micro;
using TileShop.Shared.EventModels;

namespace TileShop.WPF.ViewModels
{
    public class MenuViewModel : Screen
    {
        private IEventAggregator _events;

        public MenuViewModel(IEventAggregator events)
        {
            _events = events;
        }

        public Task OpenProject() => _events.PublishOnUIThreadAsync(new OpenProjectEvent());

        public void ExitApplication()
        {
            //await TryCloseAsync();
            Environment.Exit(0);
        }
    }
}
