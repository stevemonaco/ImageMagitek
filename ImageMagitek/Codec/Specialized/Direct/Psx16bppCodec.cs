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
    public class Psx16bppCodec : IDirectGraphicsCodec
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

        private byte[] _buffer;
        private Memory<byte> _memoryBuffer;
        private BitStream _bitStream;

        public Psx16bppCodec(int width, int height)
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
                    uint packedColor = _bitStream.ReadByte();
                    packedColor |= (uint)_bitStream.ReadByte() << 8;
                    var colorAbgr16 = ColorFactory.CreateColor(ColorModel.ABGR16, packedColor);
                    imageBuffer[x, y] = ColorConverter.ToNative(colorAbgr16);
                }
            }
        }

        public void Encode(ArrangerElement el, ColorRgba32[,] imageBuffer)
        {
            var fs = el.DataFile.Stream;

            if (el.FileAddress + StorageSize > fs.Length * 8) // Element would contain data past the end of the file
                return;

            fs.Seek(el.FileAddress.FileOffset, SeekOrigin.Begin);
            var bw = new BinaryWriter(fs);

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    var imageColor = imageBuffer[x, y];
                    var fc = ColorConverter.ToForeign(imageColor, ColorModel.ABGR16);

                    bw.Write((ushort)fc.Color);
                }
            }

            bw.Flush();
        }
    }
}
