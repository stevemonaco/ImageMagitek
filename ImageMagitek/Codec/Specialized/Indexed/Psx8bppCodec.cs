using System;

namespace ImageMagitek.Codec
{
    public sealed class Psx8bppCodec : IndexedCodec
    {
        public override string Name => "PSX 8bpp";
        public override int Width { get; } = 8;
        public override int Height { get; } = 8;
        public override ImageLayout Layout => ImageLayout.Single;
        public override int ColorDepth => 8;
        public override int StorageSize => Width * Height * 8;

        public Psx8bppCodec(int width, int height)
        {
            Width = width;
            Height = height;

            _foreignBuffer = new byte[(StorageSize + 7) / 8];
            _nativeBuffer = new byte[Width, Height];
        }

        public override byte[,] DecodeElement(ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
                throw new ArgumentException(nameof(encodedBuffer));

            int srcidx = 0;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++, srcidx++)
                {
                    var palIndex = encodedBuffer[srcidx];
                    _nativeBuffer[x, y] = palIndex;
                }
            }

            return _nativeBuffer;
        }

        public override ReadOnlySpan<byte> EncodeElement(ArrangerElement el, byte[,] imageBuffer)
        {
            if (imageBuffer.GetLength(0) != Width || imageBuffer.GetLength(1) != Height)
                throw new ArgumentException(nameof(imageBuffer));

            int destidx = 0;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++, destidx++)
                {
                    byte index = imageBuffer[x, y];
                    _foreignBuffer[destidx] = index;
                }
            }

            return ForeignBuffer;
        }
    }
}
