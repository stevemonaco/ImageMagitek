using Caliburn.Micro;
using TileShop.Shared.EventModels;
using System.Threading;
using System.Threading.Tasks;

namespace TileShop.WPF.ViewModels
{
    public class StatusBarViewModel : Screen, IHandle<NotifyStatusEvent>
    {
        private IEventAggregator _events;

        private string _activityMessage;
        public string ActivityMessage
        {
            get => _activityMessage;
            set
            {
                _activityMessage = value;
                NotifyOfPropertyChange(() => ActivityMessage);
            }
        }

        private BindableCollection<string> _timedMessages;
        public BindableCollection<string> TimedMessages
        {
            get => _timedMessages;
            set
            {
                _timedMessages = value;
                NotifyOfPropertyChange(() => TimedMessages);
            }
        } 

        public StatusBarViewModel(IEventAggregator events)
        {
            _events = events;
            _events.SubscribeOnUIThread(this);
        }

        public Task HandleAsync(NotifyStatusEvent notifyEvent, CancellationToken cancellationToken)
        {
            if (notifyEvent.DisplayDuration == NotifyStatusDuration.Indefinite)
                ActivityMessage = notifyEvent.NotifyMessage;
            else if (notifyEvent.DisplayDuration == NotifyStatusDuration.Short)
                TimedMessages.Add(notifyEvent.NotifyMessage);

            return Task.CompletedTask;
        }
    }
}
