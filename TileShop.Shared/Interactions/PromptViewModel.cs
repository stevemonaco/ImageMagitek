using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.Shared.Interactions;
public class PromptViewModel : RequestBaseViewModel<PromptResult>
{
    public string Message { get; }
    private PromptResult _result = PromptResult.Cancel;
    private readonly PromptChoice _choices;
    
    public PromptViewModel(PromptChoice choices, string title, string message)
    {
        _choices = choices;
        Title = title;
        Message = message;
        
        TryAcceptCommand = new AsyncRelayCommand(Accept, CanAccept); 
        TryCancelCommand = new AsyncRelayCommand(TryCancel, CanTryCancel); 
        TryRejectCommand = new AsyncRelayCommand(TryReject, CanTryReject);
    }

    public override ObservableCollection<RequestOption> Options { get; protected set; } = [];
    public override PromptResult ProduceResult() => _result;

    public override ObservableCollection<RequestOption> CreateOptions()
    {
        var options = new ObservableCollection<RequestOption>();
        
        if (_choices.Cancel is not null)
            options.Add(new RequestOption(_choices.Cancel, TryCancelCommand, isCancel: true));
        
        if (_choices.Reject is not null)
            options.Add(new RequestOption(_choices.Reject, TryRejectCommand, isDanger: true));
        
        if (_choices.Accept is not null)
            options.Add(new RequestOption(_choices.Accept, TryAcceptCommand, isDefault: true));

        return options;
    }

    public IAsyncRelayCommand TryAcceptCommand { get; }
    public IAsyncRelayCommand TryCancelCommand { get; }
    public IAsyncRelayCommand TryRejectCommand { get; }

    protected virtual Task<bool> TryReject()
    {
        _result = PromptResult.Reject;
        return Task.FromResult(TryClose());
    }
    
    protected virtual bool CanTryReject() => true;
}