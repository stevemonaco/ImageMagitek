using System.Drawing;
using TileShop.Shared.Models;

namespace TileShop.UI.Models;

public class ColorRemapHistoryAction : HistoryAction
{
    public override string Name => "Color Remap";

    /// <summary>
    /// New palette index per original palette index
    /// </summary>
    public byte[] Remap { get; }

    /// <summary>
    /// Pixel region the remap was limited to, or null for the entire image
    /// </summary>
    public Rectangle? Bounds { get; }

    public ColorRemapHistoryAction(byte[] remap, Rectangle? bounds)
    {
        Remap = remap;
        Bounds = bounds;
    }
}
