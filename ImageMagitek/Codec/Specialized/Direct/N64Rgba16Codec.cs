using System;
using ImageMagitek.Colors;
using ImageMagitek.Colors.Converters;

namespace ImageMagitek.Codec
{
    public sealed class N64Rgba16Codec : DirectCodec
    {
        public override string Name => "N64 Rgba16";
        public override int Width { get; } = 32;
        public override int Height { get; } = 32;
        public override ImageLayout Layout => ImageLayout.Tiled;
        public override int ColorDepth => 32;
        public override int StorageSize => Width * Height * 32;
        public override int RowStride { get; } = 0;
        public override int ElementStride { get; } = 0;

        public override bool CanResize => true;
        public override int WidthResizeIncrement => 1;
        public override int HeightResizeIncrement => 1;
        public override int DefaultWidth => 32;
        public override int DefaultHeight => 32;

        private BitStream _bitStream;
        private readonly ColorConverterAbgr16 _colorConverter = new();

        public N64Rgba16Codec()
        {
            Width = DefaultWidth;
            Height = DefaultHeight;

            _foreignBuffer = new byte[(StorageSize + 7) / 8];
            _nativeBuffer = new ColorRgba32[Height, Width];

            _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
        }

        public N64Rgba16Codec(int width, int height)
        {
            Width = width;
            Height = height;

            _foreignBuffer = new byte[(StorageSize + 7) / 8];
            _nativeBuffer = new ColorRgba32[Height, Width];

            _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
        }

        public override ColorRgba32[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            if (encodedBuffer.Length * 8 < StorageSize)
                throw new ArgumentException(nameof(encodedBuffer));

            encodedBuffer.Slice(0, _foreignBuffer.Length).CopyTo(_foreignBuffer);
            _bitStream.SeekAbsolute(0);

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    ushort pair = (ushort)(_bitStream.ReadByte() << 8 | _bitStream.ReadByte());
                    byte r = (byte)((pair >> 11) << 3);
                    byte g = (byte)(((pair >> 6) & 0x1F) << 3);
                    byte b = (byte)(((pair >> 1) & 0x1F) << 3);
                    byte a = (pair & 0x1) == 1 ? (byte)0xFF : (byte)0;

                    _nativeBuffer[y, x] = new ColorRgba32(r, g, b, a);
                }
            }

            return NativeBuffer;
        }

        public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, ColorRgba32[,] imageBuffer)
        {
            if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
                throw new ArgumentException(nameof(imageBuffer));

            var bs = BitStream.OpenWrite(StorageSize, 8);

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    var imageColor = imageBuffer[y, x];

                    ushort r = (ushort)((imageColor.R >> 3) << 11);
                    ushort g = (ushort)((imageColor.G >> 3) << 6);
                    ushort b = (ushort)((imageColor.B >> 3) << 1);
                    ushort a = imageColor.A == 255 ? (byte)1 : (byte)0;

                    ushort pair = (ushort) (r | g | b | a);
                    byte high = (byte)(pair >> 8);
                    byte low = (byte)(pair & 0xFF);

                    bs.WriteByte(high);
                    bs.WriteByte(low);
                }
            }

            return bs.Data;
        }
    }
}
