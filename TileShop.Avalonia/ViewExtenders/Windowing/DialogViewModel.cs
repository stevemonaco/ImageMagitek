using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TileShop.Shared.Interactions;

namespace TileShop.AvaloniaUI.Windowing;
public abstract partial class DialogViewModel<TResult> : ObservableValidator, IRequestMediator<TResult>
{
    [ObservableProperty] private TResult? _requestResult = default(TResult);
    [ObservableProperty] private string _title = "";

    private RelayCommand<TResult>? _okCommand;
    public IRelayCommand<TResult> AcceptCommand => _okCommand ??= new RelayCommand<TResult>(Ok);

    private RelayCommand? _cancelCommand;
    public IRelayCommand CancelCommand => _cancelCommand ??= new RelayCommand(Cancel);

    public string AcceptName { get; protected set; } = "Ok";
    public string CancelName { get; protected set; } = "Cancel";

    public virtual void Ok(TResult? result)
    {
        _requestResult = result;
        OnPropertyChanged(nameof(RequestResult)); // Explicitly raise INPC, so null/default results will return
    }
    
    public virtual void Cancel()
    {
        _requestResult = default;
        OnPropertyChanged(nameof(RequestResult));
    }
}
