using ImageMagitek.Colors;

namespace ImageMagitek.Project
{
    public class PaletteNode : ResourceNode<Palette>
    {
        public Palette Palette { get; }

        public PaletteNode(string name, Palette palette) : base(name, palette)
        {
            Palette = palette;
        }
    }
}
