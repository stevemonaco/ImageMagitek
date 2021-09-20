using System;

namespace ImageMagitek.Codec
{
    public sealed class Nes1bppCodec : IndexedCodec
    {
        public override string Name => "NES 1bpp";
        public override int Width { get; } = 8;
        public override int Height { get; } = 8;
        public override ImageLayout Layout => ImageLayout.Tiled;
        public override int ColorDepth => 1;
        public override int StorageSize => 1 * Width * Height;
        public override bool CanEncode => true;

        public override int DefaultWidth => 8;
        public override int DefaultHeight => 8;
        public override int WidthResizeIncrement => 1;
        public override int HeightResizeIncrement => 1;
        public override bool CanResize => true;

        private BitStream _bitStream;

        public Nes1bppCodec()
        {
            Width = DefaultWidth;
            Height = DefaultHeight;
            Initialize();
        }

        public Nes1bppCodec(int width, int height)
        {
            Width = width;
            Height = height;
        }

        private void Initialize()
        {
            _foreignBuffer = new byte[(StorageSize + 7) / 8];
            _nativeBuffer = new byte[Height, Width];
        }

        public override byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
                throw new ArgumentException(nameof(encodedBuffer));

            encodedBuffer.Slice(0, _foreignBuffer.Length).CopyTo(_foreignBuffer);

            _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var bp1 = _bitStream.ReadBit();
                    _nativeBuffer[y, x] = (byte) bp1;
                }
            }

            return _nativeBuffer;
        }

        public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
        {
            if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
                throw new ArgumentException(nameof(imageBuffer));

            var bs = BitStream.OpenWrite(StorageSize, 8);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var index = imageBuffer[y, x];
                    bs.WriteBit(index);
                }
            }

            return bs.Data;
        }
    }
}
