using ImageMagitek.Colors;

namespace ImageMagitek.Image
{
    public class DirectImage : ImageBase<ColorRgba32>
    {
        public DirectImage(int width, int height)
        {
            Width = width;
            Height = height;
            Image = new ColorRgba32[Width * Height];
        }
    }
}
