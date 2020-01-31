using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek.Codec
{
    public class Psx24bppCodec : IDirectGraphicsCodec
    {
        public string Name => "PSX 24bpp";

        public int Width { get; private set; } = 8;

        public int Height { get; private set; } = 8;

        public ImageLayout Layout => ImageLayout.Linear;

        public PixelColorType ColorType => PixelColorType.Direct;

        public int ColorDepth => 24;

        public int StorageSize => Width * Height * 24;

        public int RowStride { get; private set; } = 0;

        public int ElementStride { get; private set; } = 0;

        private byte[] _buffer;
        private Memory<byte> _memoryBuffer;
        private BitStream _bitStream;

        public Psx24bppCodec(int width, int height)
        {
            Width = width;
            Height = height;

            _buffer = new byte[(StorageSize + 7) / 8];
            _memoryBuffer = new Memory<byte>(_buffer);
            _bitStream = BitStream.OpenRead(_buffer, StorageSize);
        }

        public void Decode(ArrangerElement el, ColorRgba32[,] imageBuffer)
        {
            var fs = el.DataFile.Stream;

            if (el.FileAddress + StorageSize > fs.Length * 8) // Element would contain data past the end of the file
                return;

            _bitStream.SeekAbsolute(0);
            fs.ReadUnshifted(el.FileAddress, StorageSize, true, _memoryBuffer.Span);

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    byte r = _bitStream.ReadByte();
                    byte g = _bitStream.ReadByte();
                    byte b = _bitStream.ReadByte();

                    imageBuffer[x, y] = new ColorRgba32(r, g, b, 0xFF);
                }
            }
        }

        public void Encode(ArrangerElement el, ColorRgba32[,] imageBuffer)
        {
            var fs = el.DataFile.Stream;

            if (el.FileAddress + StorageSize > fs.Length * 8) // Element would contain data past the end of the file
                return;

            fs.Seek(el.FileAddress.FileOffset, SeekOrigin.Begin);

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    var imageColor = imageBuffer[x, y];
                    fs.WriteByte(imageColor.R);
                    fs.WriteByte(imageColor.G);
                    fs.WriteByte(imageColor.B);
                }
            }

            fs.Flush();
        }
    }
}
