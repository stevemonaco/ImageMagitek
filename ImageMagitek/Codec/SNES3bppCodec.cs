using System;
using System.Collections.Generic;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek
{
    public sealed class SNES3bppCodec : IGraphicsCodec
    {
        public string Name => "SNES3bpp";
        public int Width { get; private set; } = 8;
        public int Height { get; private set; } = 8;
        public int StorageSize => 3 * Width * Height;
        public ImageLayout Layout => ImageLayout.Tiled;
        public PixelColorType ColorType => PixelColorType.Indexed;
        public int ColorDepth => 3;
        public int RowStride => 0;
        public int ElementStride => 0;

        public SNES3bppCodec(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public void Decode(Image<Rgba32> image, ArrangerElement el)
        {
            var fs = el.DataFile.Stream;

            if (el.FileAddress + StorageSize > fs.Length * 8) // Element would contain data past the end of the file
                return;

            var data = fs.ReadUnshifted(el.FileAddress, StorageSize, true);
            var bs = BitStream.OpenRead(data, StorageSize);

            var offsetPlane1 = 0;
            var offsetPlane2 = el.Width;
            var offsetPlane3 = el.Width * el.Height * 2;

            for(int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    bs.SeekAbsolute(offsetPlane1);
                    var bp1 = bs.ReadBit();
                    bs.SeekAbsolute(offsetPlane2);
                    var bp2 = bs.ReadBit();
                    bs.SeekAbsolute(offsetPlane3);
                    var bp3 = bs.ReadBit();

                    var palIndex = (bp1 << 0) | (bp2 << 1) | (bp3 << 2);
                    image[el.X1 + x, el.Y1 + y] = el.Palette[palIndex].ToRgba32();

                    offsetPlane1++;
                    offsetPlane2++;
                    offsetPlane3++;
                }
                offsetPlane1 += Width;
                offsetPlane2 += Width;
            }
        }

        public void Encode(Image<Rgba32> image, ArrangerElement el)
        {
            throw new NotImplementedException();
        }
    }
}
