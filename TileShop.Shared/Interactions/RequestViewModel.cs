using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.Shared.Interactions;

/// <summary>
/// A request dialog that can be used to prompt the user for an accept / cancel response
/// </summary>
public abstract class RequestViewModel<TResult> : RequestBaseViewModel<TResult>
{
    public override ObservableCollection<RequestOption> Options { get; protected set; } = [];
    
    public override TResult? ProduceResult() => RequestResult;

    public override ObservableCollection<RequestOption> CreateOptions()
    {
        return new()
        {
            new RequestOption(CancelName, TryCancelCommand!)
            {
                IsCancel = true
            },
            new RequestOption(AcceptName, TryAcceptCommand!)
            {
                IsDefault = true
            },
        };
    }

    public IAsyncRelayCommand TryAcceptCommand => field ??= new AsyncRelayCommand(Accept, CanAccept);
    public IAsyncRelayCommand TryCancelCommand => field ??= new AsyncRelayCommand(TryCancel, CanTryCancel);
}