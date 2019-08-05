using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek.Codec.Specialized
{
    public class Psx16bppCodec : IGraphicsCodec
    {
        public string Name => "PSX 16bpp";

        public int Width { get; private set; } = 8;

        public int Height { get; private set; } = 8;

        public ImageLayout Layout => ImageLayout.Linear;

        public PixelColorType ColorType => PixelColorType.Direct;

        public int ColorDepth => 16;

        public int StorageSize => Width * Height * 16;

        public int RowStride { get; private set; } = 0;

        public int ElementStride { get; private set; } = 0;

        public Psx16bppCodec(int width, int height)
        {
            Width = width;
            Height = height;
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

            var pal = el.Palette;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    // Need Abgr15
                    uint packedColor = (uint) bs.ReadBits(16);
                    var colorAbgr16 = ColorFactory.CreateColor(ColorModel.ABGR16, packedColor);
                    dest[destidx] = ColorConverter.ToNative(colorAbgr16).ToRgba32();
                    destidx++;
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
            var bw = new BinaryWriter(fs);

            var src = image.GetPixelSpan();
            int srcidx = image.Width * el.Y1 + el.X1;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    var imageColor = src[srcidx];
                    var nc = new ColorRgba32(imageColor.PackedValue);
                    var fc = ColorConverter.ToForeign(nc, ColorModel.ABGR16);

                    bw.Write((ushort)fc.Color);
                    srcidx++;
                }
                srcidx += RowStride + el.X1 + image.Width - (el.X2 + 1);
            }

            bw.Flush();
        }
    }
}
