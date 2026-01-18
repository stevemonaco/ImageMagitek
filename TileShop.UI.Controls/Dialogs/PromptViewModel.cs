namespace TileShop.UI.Controls.Dialogs;

public class PromptViewModel : RequestBaseViewModel<bool>
{
    public string Message { get; }

    public PromptViewModel(string title, string message)
    {
        Title = title;
        Message = message;
    }

    public override bool ProduceResult() => true;

    // protected override void Accept()
    // {
    // }
}