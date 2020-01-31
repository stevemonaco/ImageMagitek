using ImageMagitek.Colors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek.Codec
{
    public sealed class BlankCodec : IGraphicsCodec, IDirectGraphicsCodec, IIndexedGraphicsCodec
    {
        public string Name => "Blank";

        public int StorageSize => 0;
        public int Width => 0;
        public int Height => 0;
        public ImageLayout Layout => ImageLayout.Tiled;
        public PixelColorType ColorType => PixelColorType.Direct;
        public int ColorDepth => 1;
        public int RowStride => 0;
        public int ElementStride => 0;

        private Rgba32 FillColor = Rgba32.Black;

        public BlankCodec() { }

        public BlankCodec(Rgba32 fillColor)
        {
            FillColor = fillColor;
        }

        public void Decode(Image<Rgba32> image, ArrangerElement el)
        {
            var dest = image.GetPixelSpan();
            int destidx = image.Width * el.Y1 + el.X1;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++, destidx++)
                    dest[destidx] = FillColor;
                destidx += el.X1 + image.Width - (el.X2 + 1);
            }
        }

        public void Encode(Image<Rgba32> image, ArrangerElement el) { }

        public void Decode(ArrangerElement el, ColorRgba32[,] imageBuffer)
        {
            var color = new ColorRgba32(FillColor.R, FillColor.G, FillColor.B, FillColor.A);

            for(int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                    imageBuffer[x, y] = color;
            }
        }

        public void Encode(ArrangerElement el, ColorRgba32[,] imageBuffer) { }

        public void Decode(ArrangerElement el, byte[,] imageBuffer)
        {
            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                    imageBuffer[x, y] = 0;
            }
        }

        public void Encode(ArrangerElement el, byte[,] imageBuffer) { }
    }
}
