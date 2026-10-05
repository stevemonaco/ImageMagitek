using System.Drawing;
using ImageMagitek;
using TileShop.Shared.Models;

namespace TileShop.UI.Models;

public class PasteArrangerHistoryAction : HistoryAction
{
    public override string Name => "Paste Arranger";

    public ArrangerCopy Copy { get; }
    public int X { get; }
    public int Y { get; }
    public Rectangle? ClipBounds { get; }

    public PasteArrangerHistoryAction(ArrangerCopy copy, int x, int y, Rectangle? clipBounds)
    {
        Copy = copy;
        X = x;
        Y = y;
        ClipBounds = clipBounds;
    }
}
