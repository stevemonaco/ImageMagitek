namespace TileShop.Shared.Interactions;
public class AlertViewModel : RequestViewModel<bool>
{
    public string Message { get; }

    public AlertViewModel(string title, string message)
    {
        Title = title;
        Message = message;
    }

    public override bool ProduceResult() => true;
}