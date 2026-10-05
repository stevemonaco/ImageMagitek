using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using TileShop.Shared.Messages;

namespace TileShop.UI.ViewModels;

public partial class StatusViewModel : ObservableRecipient
{
    private readonly DispatcherTimer _clearTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private EditorsViewModel _editors;

    public StatusViewModel(EditorsViewModel editors)
    {
        Messenger.Register<NotifyStatusMessage>(this, (r, m) => Receive(m));
        _editors = editors;

        _clearTimer.Tick += (_, _) =>
        {
            _clearTimer.Stop();
            StatusMessage = "";
        };
    }

    public void Receive(NotifyStatusMessage message)
    {
        _clearTimer.Stop();
        StatusMessage = message.DisplayDuration == NotifyStatusDuration.Reset ? "" : message.NotifyMessage;

        if (message.DisplayDuration == NotifyStatusDuration.Short)
            _clearTimer.Start();
    }
}
