using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using TileShop.Shared.EventModels;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class StatusViewModel : ObservableRecipient
{
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private EditorsViewModel _editors;

    public StatusViewModel(EditorsViewModel editors)
    {
        Messenger.Register<NotifyStatusEvent>(this, (r, m) => Receive(m));
        _editors = editors;
    }

    public async void Receive(NotifyStatusEvent notifyEvent)
    {
        if (notifyEvent.DisplayDuration == NotifyStatusDuration.Short)
        {
            StatusMessage = notifyEvent.NotifyMessage;
            await Task.Delay(2000);
            if (StatusMessage == notifyEvent.NotifyMessage)
                StatusMessage = "";
        }
    }
}
