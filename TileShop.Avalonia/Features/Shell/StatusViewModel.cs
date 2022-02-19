﻿using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using TileShop.Shared.EventModels;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class StatusViewModel : ObservableRecipient
{
    [ObservableProperty] private string? _activityMessage;
    [ObservableProperty] private string? _operationMessage;
    [ObservableProperty] private ObservableCollection<string> _timedMessages = new();

    public StatusViewModel()
    {
        WeakReferenceMessenger.Default.Register<StatusViewModel, NotifyStatusEvent>(this, (r, m) => r.Receive(m));
        WeakReferenceMessenger.Default.Register<StatusViewModel, NotifyOperationEvent>(this, (r, m) => r.Receive(m));
    }

    public void Receive(NotifyStatusEvent notifyEvent)
    {
        if (notifyEvent.DisplayDuration == NotifyStatusDuration.Indefinite)
            ActivityMessage = notifyEvent.NotifyMessage;
        else if (notifyEvent.DisplayDuration == NotifyStatusDuration.Short)
            TimedMessages.Add(notifyEvent.NotifyMessage);
    }

    public void Receive(NotifyOperationEvent notifyEvent)
    {
        OperationMessage = notifyEvent.NotifyMessage;
    }
}