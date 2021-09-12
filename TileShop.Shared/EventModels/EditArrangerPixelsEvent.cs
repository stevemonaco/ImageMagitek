using ImageMagitek;
namespace TileShop.Shared.EventModels
{
    public record EditArrangerPixelsEvent(Arranger Arranger, Arranger ProjectArranger, int X, int Y, int Width, int Height);
}
