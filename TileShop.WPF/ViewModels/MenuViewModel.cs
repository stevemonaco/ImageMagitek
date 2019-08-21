using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TileShop.WPF.EventModels;

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
