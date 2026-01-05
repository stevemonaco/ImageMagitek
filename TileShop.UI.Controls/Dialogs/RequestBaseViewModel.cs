using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TileShop.Shared.Interactions;

namespace TileShop.UI.Controls;
public abstract partial class RequestBaseViewModel<TResult> : ObservableValidator, IRequestMediator<TResult>
{
    public TResult? RequestResult { get; protected set; }
    
    [ObservableProperty] private string _title = "";

    private RelayCommand? _acceptCommand;
    public IRelayCommand AcceptCommand => _acceptCommand ??= new RelayCommand(Accept, CanAccept);

    private RelayCommand? _cancelCommand;
    public IRelayCommand CancelCommand => _cancelCommand ??= new RelayCommand(Cancel);
    
    public event EventHandler<CancelEventArgs>? Closing;
    public event EventHandler? Closed;

    public string AcceptName { get; protected set; } = "Ok";
    public string CancelName { get; protected set; } = "Cancel";
    
    public abstract TResult? ProduceResult();

    /// <summary>
    /// Called when the user accepts the interaction
    /// Responsible for mapping an internal result into RequestResult
    /// </summary>
    protected abstract void Accept();

    protected virtual bool CanAccept() => true;

    /// <summary>
    /// Called when the user cancels an interaction
    /// </summary>
    protected virtual void Cancel()
    {
        RequestResult = default;
        OnPropertyChanged(nameof(RequestResult));
    }
}
