namespace TileShop.WPF.Models;

public class PasteArrangerHistoryAction : HistoryAction
{
    public override string Name => "Paste Arranger";

    public ArrangerPaste Paste { get; }

    public PasteArrangerHistoryAction(ArrangerPaste paste)
    {
        Paste = paste;
    }
}
