using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.Shared.Interactions;
public interface IRequestMediator<out TResult> : INotifyPropertyChanged
{
    string Title { get; }
    // string AcceptName { get; }
    // string CancelName { get; }
    TResult? RequestResult { get; }

    // IAsyncRelayCommand TryAcceptCommand { get; }
    // IAsyncRelayCommand TryCancelCommand { get; }
    
    ObservableCollection<RequestOption> Options { get; }

    Task OnOpening();
    Task<bool> TryCancel();
    
    event EventHandler<CancelEventArgs>? Closing;
    event EventHandler? Closed;
}
