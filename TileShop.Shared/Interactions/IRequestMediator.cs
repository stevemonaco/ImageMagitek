using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.Shared.Interactions;
public interface IRequestMediator<TResult> : INotifyPropertyChanged
{
    string Title { get; }
    string AcceptName { get; }
    string CancelName { get; }
    TResult? RequestResult { get; set; }

    IRelayCommand<TResult> AcceptCommand { get; }
    IRelayCommand CancelCommand { get; }
}
