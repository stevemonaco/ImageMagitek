using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.AvaloniaUI.ViewExtenders;
public interface IDialogMediator<TResult> : INotifyPropertyChanged
{
    TResult? DialogResult { get; set; }

    IRelayCommand<TResult> OkCommand { get; }
    IRelayCommand CancelCommand { get; }
}
