using ImageMagitek;

namespace TileShop.Shared.Models;

public class MirrorElementHistoryAction : HistoryAction
{
    public override string Name => "Mirror Element";

    public MirrorOperation Mirror { get; }
    public int ElementX { get; }
    public int ElementY { get; }

    public MirrorElementHistoryAction(int elementX, int elementY, MirrorOperation mirror)
    {
        ElementX = elementX;
        ElementY = elementY;
        Mirror = mirror;
    }
}
