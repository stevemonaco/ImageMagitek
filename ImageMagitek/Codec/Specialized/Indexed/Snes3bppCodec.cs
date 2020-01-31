using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Codec
{
    public sealed class Snes3bppCodec : IIndexedGraphicsCodec
    {
        public string Name => "SNES 3bpp";
        public int Width { get; private set; } = 8;
        public int Height { get; private set; } = 8;
        public int StorageSize => 3 * Width * Height;
        public ImageLayout Layout => ImageLayout.Tiled;
        public PixelColorType ColorType => PixelColorType.Indexed;
        public int ColorDepth => 3;
        public int RowStride => 0;
        public int ElementStride => 0;

        private byte[] _buffer;
        private Memory<byte> _memoryBuffer;
        private BitStream _bitStream;

        public Snes3bppCodec(int width, int height)
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

            var offsetPlane1 = 0;
            var offsetPlane2 = el.Width;
            var offsetPlane3 = el.Width * el.Height * 2;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    _bitStream.SeekAbsolute(offsetPlane1);
                    var bp1 = _bitStream.ReadBit();
                    _bitStream.SeekAbsolute(offsetPlane2);
                    var bp2 = _bitStream.ReadBit();
                    _bitStream.SeekAbsolute(offsetPlane3);
                    var bp3 = _bitStream.ReadBit();

                    var palIndex = (bp1 << 0) | (bp2 << 1) | (bp3 << 2);
                    imageBuffer[x, y] = (byte) palIndex;

                    offsetPlane1++;
                    offsetPlane2++;
                    offsetPlane3++;
                }

                offsetPlane1 += Width;
                offsetPlane2 += Width;
            }
        }

        public void Encode(ArrangerElement el, byte[,] imageBuffer)
        {
            var fs = el.DataFile.Stream;

            if (el.FileAddress + StorageSize > fs.Length * 8) // Ensure there is enough data to decode
                return;

            var bs = BitStream.OpenWrite(StorageSize, 8);

            var offsetPlane1 = 0;
            var offsetPlane2 = el.Width;
            var offsetPlane3 = el.Width * el.Height * 2;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    var index = imageBuffer[x, y];

                    byte bp1 = (byte)(index & 1);
                    byte bp2 = (byte)((index >> 1) & 1);
                    byte bp3 = (byte)((index >> 2) & 1);

                    bs.SeekAbsolute(offsetPlane1);
                    bs.WriteBit(bp1);
                    bs.SeekAbsolute(offsetPlane2);
                    bs.WriteBit(bp2);
                    bs.SeekAbsolute(offsetPlane3);
                    bs.WriteBit(bp3);

                    offsetPlane1++;
                    offsetPlane2++;
                    offsetPlane3++;
                }
                offsetPlane1 += Width;
                offsetPlane2 += Width;
            }

            fs.Seek(el.FileAddress.FileOffset, System.IO.SeekOrigin.Begin);
            fs.Write(bs.Data, 0, bs.Data.Length);
        }
    }
}
