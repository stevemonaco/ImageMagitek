using ImageMagitek.Colors;

namespace TileShop.Shared.EventModels
{
    public class PaletteChangedEvent
    {
        public Palette Palette { get; }

        public PaletteChangedEvent(Palette palette)
        {
            Palette = palette;
        }
    }
}
