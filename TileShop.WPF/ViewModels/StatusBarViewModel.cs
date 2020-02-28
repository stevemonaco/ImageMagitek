using Stylet;
using TileShop.Shared.EventModels;

namespace TileShop.WPF.ViewModels
{
    public class StatusBarViewModel : Screen, IHandle<NotifyStatusEvent>
    {
        private IEventAggregator _events;

        private string _activityMessage;
        public string ActivityMessage
        {
            get => _activityMessage;
            set => SetAndNotify(ref _activityMessage, value);
        }

        private BindableCollection<string> _timedMessages;
        public BindableCollection<string> TimedMessages
        {
            get => _timedMessages;
            set => SetAndNotify(ref _timedMessages, value);
        } 

        public StatusBarViewModel(IEventAggregator events)
        {
            _events = events;
            _events.Subscribe(this);
        }

        public void Handle(NotifyStatusEvent notifyEvent)
        {
            if (notifyEvent.DisplayDuration == NotifyStatusDuration.Indefinite)
                ActivityMessage = notifyEvent.NotifyMessage;
            else if (notifyEvent.DisplayDuration == NotifyStatusDuration.Short)
                TimedMessages.Add(notifyEvent.NotifyMessage);
        }
    }
}
