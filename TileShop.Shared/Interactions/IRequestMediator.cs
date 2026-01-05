using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.Shared.Interactions;
public interface IRequestMediator<TResult> : INotifyPropertyChanged
{
    string Title { get; }
    string AcceptName { get; }
    string CancelName { get; }
    TResult? RequestResult { get; }

    IRelayCommand AcceptCommand { get; }
    IRelayCommand CancelCommand { get; }
    
    public event EventHandler<CancelEventArgs>? Closing;
    public event EventHandler? Closed;
}
