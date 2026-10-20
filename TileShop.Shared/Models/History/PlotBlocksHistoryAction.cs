using ImageMagitek.Metatiles;

namespace TileShop.Shared.Models;

/// <summary>
/// A Blocks mode stroke, fill or erase on a metatile map's block grid, such as "Plot Blocks", "Fill Blocks" or "Erase Blocks"
/// </summary>
public class PlotBlocksHistoryAction(string name, BlockGrid after) : HistoryAction
{
    public override string Name => name;

    /// <summary>
    /// A copy of the block grid after the action
    /// </summary>
    public BlockGrid After => after;
}
