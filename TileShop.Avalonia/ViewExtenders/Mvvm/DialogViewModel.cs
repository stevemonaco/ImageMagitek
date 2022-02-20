using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.AvaloniaUI.ViewExtenders;
public abstract partial class DialogViewModel<TResult> : ObservableRecipient
{
    [ObservableProperty] private TResult? _dialogResult = default(TResult);

    [ICommand]
    public virtual void Close(TResult? result)
    {
        if (result is null)
            OnPropertyChanged(nameof(DialogResult));
        else
            DialogResult = result;
    }
}
