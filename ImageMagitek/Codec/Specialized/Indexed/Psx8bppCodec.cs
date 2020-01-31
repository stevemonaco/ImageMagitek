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
    public class Psx8bppCodec : IIndexedGraphicsCodec
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

        private byte[] _buffer;
        private Memory<byte> _memoryBuffer;
        private BitStream _bitStream;

        public Psx8bppCodec(int width, int height)
        {
            Width = width;
            Height = height;

            _buffer = new byte[(StorageSize + 7) / 8];
            _memoryBuffer = new Memory<byte>(_buffer);
            _bitStream = BitStream.OpenRead(_buffer, StorageSize);
        }

        public void Decode(ArrangerElement el, byte[,] imageBuffer)
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
                    var palIndex = (byte)_bitStream.ReadByte();
                    imageBuffer[x, y] = palIndex;
                }
            }
        }

        public void Encode(ArrangerElement el, byte[,] imageBuffer)
        {
            var fs = el.DataFile.Stream;

            if (el.FileAddress + StorageSize > fs.Length * 8) // Element would contain data past the end of the file
                return;

            fs.Seek(el.FileAddress.FileOffset, SeekOrigin.Begin);

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    byte index = imageBuffer[x, y];
                    fs.WriteByte(index);
                }
            }

            fs.Flush();
        }
    }
}
