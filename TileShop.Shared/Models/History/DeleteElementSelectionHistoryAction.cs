namespace TileShop.Shared.Models;

public class DeleteElementSelectionHistoryAction : HistoryAction
{
    public override string Name => "Delete Selection";

    public SnappedRectangle Rect { get; }

    public DeleteElementSelectionHistoryAction(SnappedRectangle rect)
    {
        Rect = rect;
    }
}
