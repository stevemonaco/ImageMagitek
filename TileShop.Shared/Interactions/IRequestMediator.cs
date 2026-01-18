using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.Shared.Interactions;
public interface IRequestMediator<out TResult> : INotifyPropertyChanged
{
    string Title { get; }
    string AcceptName { get; }
    string CancelName { get; }
    TResult? Result { get; }

    IAsyncRelayCommand TryAcceptCommand { get; }
    IAsyncRelayCommand TryCancelCommand { get; }
    
    event EventHandler<CancelEventArgs>? Closing;
    event EventHandler? Closed;
}
