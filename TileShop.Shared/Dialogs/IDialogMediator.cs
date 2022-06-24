using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.Shared.Dialogs;
public interface IDialogMediator<TResult> : INotifyPropertyChanged
{
    TResult? DialogResult { get; set; }

    IRelayCommand<TResult> OkCommand { get; }
    IRelayCommand CancelCommand { get; }
}
