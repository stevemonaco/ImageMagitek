using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek.Codec
{
    public class Psx4bppCodec : IGraphicsCodec
    {
        public string Name => "PSX 4bpp";

        public int Width { get; private set; } = 8;

        public int Height { get; private set; } = 8;

        public ImageLayout Layout => ImageLayout.Linear;

        public PixelColorType ColorType => PixelColorType.Indexed;

        public int ColorDepth => 4;

        public int StorageSize => Width * Height * 4;

        public int RowStride { get; private set; } = 0;

        public int ElementStride { get; private set; } = 0;
        public Palette DefaultPalette { get; set; }

        public Psx4bppCodec(int width, int height, Palette defaultPalette)
        {
            Width = width;
            Height = height;
            DefaultPalette = defaultPalette;
        }

        public void Decode(Image<Rgba32> image, ArrangerElement el)
        {
            var fs = el.DataFile.Stream;

            if (el.FileAddress + StorageSize > fs.Length * 8) // Element would contain data past the end of the file
                return;

            var dest = image.GetPixelSpan();
            int destidx = image.Width * el.Y1 + el.X1;

            var data = fs.ReadUnshifted(el.FileAddress, StorageSize, true);
            var bs = BitStream.OpenRead(data, StorageSize);

            var pal = el.Palette ?? DefaultPalette;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width / 2; x++)
                {
                    var palIndex = bs.ReadBits(4);
                    dest[destidx+1] = pal[palIndex].ToRgba32();

                    palIndex = bs.ReadBits(4);
                    dest[destidx] = pal[palIndex].ToRgba32();
                    destidx += 2;
                }

                destidx += RowStride + el.X1 + image.Width - (el.X2 + 1);
            }
        }

        public void Encode(Image<Rgba32> image, ArrangerElement el)
        {
            var fs = el.DataFile.Stream;

            if (el.FileAddress + StorageSize > fs.Length * 8) // Element would contain data past the end of the file
                return;

            fs.Seek(el.FileAddress.FileOffset, SeekOrigin.Begin);

            var src = image.GetPixelSpan();
            int srcidx = image.Width * el.Y1 + el.X1;
            var pal = el.Palette ?? DefaultPalette;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width / 2; x++)
                {
                    var imageColor = src[srcidx];
                    var nc = new ColorRgba32(imageColor.PackedValue);

                    byte indexLow = pal.GetIndexByNativeColor(nc, true);

                    imageColor = src[srcidx + 1];
                    nc = new ColorRgba32(imageColor.PackedValue);
                    byte indexHigh = pal.GetIndexByNativeColor(nc, true);

                    byte index = (byte)(indexLow | (indexHigh << 4));
                    fs.WriteByte(index);

                    srcidx++;
                }
                srcidx += RowStride + el.X1 + image.Width - (el.X2 + 1);
            }

            fs.Flush();
        }
    }
}
