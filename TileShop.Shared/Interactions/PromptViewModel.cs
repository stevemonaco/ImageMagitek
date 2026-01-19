using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.Shared.Interactions;
public class PromptViewModel : RequestViewModel<PromptResult>
{
    public string Message { get; }
    private PromptResult _result = PromptResult.Cancel;
    private readonly PromptChoice _choices;
    
    public PromptViewModel(PromptChoice choices, string title, string message)
    {
        _choices = choices;
        Title = title;
        Message = message;

        Options = [];
        
        if (choices.Cancel is not null)
            Options.Add(new RequestOption(choices.Cancel, TryCancelCommand));
        
        if (choices.Reject is not null)
            Options.Add(new RequestOption(choices.Reject, TryRejectCommand!));
        
        if (choices.Accept is not null)
            Options.Add(new RequestOption(choices.Accept, TryAcceptCommand));
    }

    public override PromptResult ProduceResult() => _result;

    public override ObservableCollection<RequestOption> CreateOptions()
    {
        var options = new ObservableCollection<RequestOption>();
        
        if (_choices.Cancel is not null)
            options.Add(new RequestOption(_choices.Cancel, TryCancelCommand));
        
        if (_choices.Reject is not null)
            options.Add(new RequestOption(_choices.Reject, TryRejectCommand!));
        
        if (_choices.Accept is not null)
            options.Add(new RequestOption(_choices.Accept, TryAcceptCommand));

        return options;
    }

    public IAsyncRelayCommand TryRejectCommand => field ??= new AsyncRelayCommand(TryReject, CanTryReject);

    protected virtual Task TryReject()
    {
        _result = PromptResult.Reject;
        return Task.CompletedTask;
    }
    
    protected virtual bool CanTryReject() => true;
}