using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.Shared.Interactions;
public interface IRequestMediator<out TResult> : INotifyPropertyChanged
{
    string Title { get; }
    TResult? RequestResult { get; }

    ObservableCollection<RequestOption> Options { get; }

    Task OnOpening();
    Task<bool> TryCancel();
    
    event EventHandler<CancelEventArgs>? Closing;
    event EventHandler? Closed;
}
