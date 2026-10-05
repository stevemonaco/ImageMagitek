namespace TileShop.Shared.Models;

/// <summary>
/// History action that changes the arranger's layout and is restored from a snapshot of the arranger taken after it ran
/// </summary>
public abstract class ArrangerHistoryAction : HistoryAction
{
    public ArrangerSnapshot? After { get; set; }
}
