using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using TileShop.Shared.Messages;

namespace TileShop.UI.ViewModels;

public partial class StatusViewModel : ObservableRecipient
{
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private EditorsViewModel _editors;

    public StatusViewModel(EditorsViewModel editors)
    {
        Messenger.Register<NotifyStatusMessage>(this, (r, m) => Receive(m));
        _editors = editors;
    }

    public async void Receive(NotifyStatusMessage message)
    {
        if (message.DisplayDuration == NotifyStatusDuration.Short)
        {
            StatusMessage = message.NotifyMessage;
            await Task.Delay(2000);
            if (StatusMessage == message.NotifyMessage)
                StatusMessage = "";
        }
    }
}
