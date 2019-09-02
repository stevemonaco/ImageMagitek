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
    public class Psx8bppCodec : IGraphicsCodec
    {
        public string Name => "PSX 8bpp";

        public int Width { get; private set; } = 8;

        public int Height { get; private set; } = 8;

        public ImageLayout Layout => ImageLayout.Linear;

        public PixelColorType ColorType => PixelColorType.Indexed;

        public int ColorDepth => 8;

        public int StorageSize => Width * Height * 8;

        public int RowStride { get; private set; } = 0;

        public int ElementStride { get; private set; } = 0;
        public Palette DefaultPalette { get; set; }

        private byte[] _buffer;
        private Memory<byte> _memoryBuffer;
        private BitStream _bitStream;

        public Psx8bppCodec(int width, int height, Palette defaultPalette)
        {
            Width = width;
            Height = height;
            DefaultPalette = defaultPalette;

            _buffer = new byte[(StorageSize + 7) / 8];
            _memoryBuffer = new Memory<byte>(_buffer);
            _bitStream = BitStream.OpenRead(_buffer, StorageSize);
        }

        public void Decode(Image<Rgba32> image, ArrangerElement el)
        {
            var fs = el.DataFile.Stream;

            if (el.FileAddress + StorageSize > fs.Length * 8) // Element would contain data past the end of the file
                return;

            var dest = image.GetPixelSpan();
            int destidx = image.Width * el.Y1 + el.X1;

            _bitStream.SeekAbsolute(0);
            fs.ReadUnshifted(el.FileAddress, StorageSize, true, _memoryBuffer.Span);

            var pal = el.Palette ?? DefaultPalette;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    var palIndex = _bitStream.ReadByte();
                    dest[destidx] = pal[palIndex].ToRgba32();
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

            var src = image.GetPixelSpan();
            int srcidx = image.Width * el.Y1 + el.X1;

            var pal = el.Palette ?? DefaultPalette;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    var imageColor = src[srcidx];
                    var nc = new ColorRgba32(imageColor.PackedValue);
                    byte index = pal.GetIndexByNativeColor(nc, true);

                    fs.WriteByte(index);
                    srcidx++;
                }
                srcidx += RowStride + el.X1 + image.Width - (el.X2 + 1);
            }

            fs.Flush();
        }
    }
}
