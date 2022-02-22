using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.AvaloniaUI.ViewExtenders;
public abstract partial class DialogViewModel<TResult> : ObservableRecipient, IDialogMediator<TResult>
{
    [ObservableProperty] private TResult? _dialogResult = default(TResult);
    [ObservableProperty] protected string? _title;

    private RelayCommand<TResult>? okCommand;
    public IRelayCommand<TResult> OkCommand => okCommand ??= new RelayCommand<TResult>(Ok);

    private RelayCommand? cancelCommand;
    public IRelayCommand CancelCommand => cancelCommand ??= new RelayCommand(Cancel);

    public virtual void Ok(TResult? result)
    {
        _dialogResult = result;
        OnPropertyChanged(nameof(DialogResult)); // Explicitly raise INPC, so null/default results will return
    }
    
    public virtual void Cancel()
    {
        _dialogResult = default;
        OnPropertyChanged(nameof(DialogResult));
    }
}
