using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TileShop.Shared.Interactions;

namespace TileShop.UI.Controls;
public abstract partial class RequestBaseViewModel<TResult> : ObservableValidator, IRequestMediator<TResult>
{
    public TResult? Result { get; protected set; }
    
    [ObservableProperty] private string _title = "";

    private AsyncRelayCommand? _acceptCommand;
    public IAsyncRelayCommand TryAcceptCommand => _acceptCommand ??= new AsyncRelayCommand(Accept, CanAccept);

    private AsyncRelayCommand? _cancelCommand;
    public IAsyncRelayCommand TryCancelCommand => _cancelCommand ??= new AsyncRelayCommand(TryCancel, CanTryCancel);
    
    public event EventHandler<CancelEventArgs>? Closing;
    public event EventHandler? Closed;

    public string AcceptName { get; protected set; } = "Ok";
    public string CancelName { get; protected set; } = "Cancel";
    
    public abstract TResult? ProduceResult();

    /// <summary>
    /// Called when the user accepts the interaction
    /// Responsible for mapping an internal result into RequestResult
    /// </summary>
    protected async Task Accept()
    {
        var cancelArgs = new CancelEventArgs();
        Closing?.Invoke(this, cancelArgs);

        if (cancelArgs.Cancel)
            return;
        
        Result = ProduceResult();
        OnPropertyChanged(nameof(Result));
        
        var hasAccepted = await OnAccepting();
        
        if (hasAccepted)
            Closed?.Invoke(this, EventArgs.Empty);
    }

    protected virtual bool CanAccept() => true;

    protected virtual Task<bool> OnAccepting() => Task.FromResult(true);

    /// <summary>
    /// Called when the user cancels an interaction
    /// </summary>
    protected virtual Task<bool> TryCancel()
    {
        var cancelArgs = new CancelEventArgs();
        Closing?.Invoke(this, cancelArgs);
        
        if (cancelArgs.Cancel)
            return Task.FromResult(false);
        
        Result = default;
        OnPropertyChanged(nameof(Result));
        
        Closed?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(true);
    }
    
    protected virtual bool CanTryCancel() => true;
    
    protected virtual bool TryClose()
    {
        var cancelArgs = new CancelEventArgs();
        Closing?.Invoke(this, cancelArgs);
        
        if (cancelArgs.Cancel)
            return false;
        
        Result = ProduceResult();
        OnPropertyChanged(nameof(Result));
        
        Closed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
