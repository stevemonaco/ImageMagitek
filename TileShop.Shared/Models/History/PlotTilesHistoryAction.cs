namespace TileShop.Shared.Models;

/// <summary>
/// A Tiles mode stroke, fill or erase on a tilemap's working copy, such as "Plot Tiles", "Fill Tiles" or "Erase Tiles"
/// </summary>
public class PlotTilesHistoryAction(string name) : ArrangerHistoryAction
{
    public override string Name => name;
}
