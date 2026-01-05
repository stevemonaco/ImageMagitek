namespace TileShop.UI.Controls.Dialogs;

public class AlertViewModel : RequestBaseViewModel<bool>
{
    public string Message { get; }

    public AlertViewModel(string title, string message)
    {
        Title = title;
        Message = message;
    }

    public override bool ProduceResult() => true;
    protected override void Accept() => Accept();
}