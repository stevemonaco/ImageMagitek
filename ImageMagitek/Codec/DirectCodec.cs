using System;
using System.IO;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek.Codec
{
    public interface IDirectCodec : IGraphicsCodec<ColorRgba32> { }

    public abstract class DirectCodec : IDirectCodec
    {
        public abstract string Name { get; }
        public abstract int Width { get; }
        public abstract int Height { get; }
        public abstract ImageLayout Layout { get; }
        public PixelColorType ColorType => PixelColorType.Direct;
        public abstract int ColorDepth { get; }
        public abstract int StorageSize { get; }
        public int RowStride { get; }
        public int ElementStride { get; }

        public virtual ReadOnlySpan<byte> ForeignBuffer => _foreignBuffer;
        protected byte[] _foreignBuffer;

        public virtual ColorRgba32[,] NativeBuffer => _nativeBuffer;
        protected ColorRgba32[,] _nativeBuffer;

        public abstract ColorRgba32[,] DecodeElement(ArrangerElement el, ReadOnlySpan<byte> encodedBuffer);

        public abstract ReadOnlySpan<byte> EncodeElement(ArrangerElement el, ColorRgba32[,] imageBuffer);

        /// <summary>
        /// Reads a contiguous block of encoded pixel data
        /// </summary>
        public virtual ReadOnlySpan<byte> ReadElement(ArrangerElement el)
        {
            var buffer = new byte[(StorageSize + 7) / 8];
            var bitStream = BitStream.OpenRead(buffer, StorageSize);

            var fs = el.DataFile.Stream;

            // TODO: Add bit granularity to seek and read
            if (el.FileAddress + StorageSize > fs.Length * 8)
                return null;

            bitStream.SeekAbsolute(0);
            fs.ReadUnshifted(el.FileAddress, StorageSize, buffer);

            return buffer;
        }

        /// <summary>
        /// Writes a contiguous block of encoded pixel data
        /// </summary>
        public virtual void WriteElement(ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            // TODO: Add bit granularity to seek and read
            var fs = el.DataFile.Stream;
            fs.Seek(el.FileAddress.FileOffset, SeekOrigin.Begin);
            fs.Write(encodedBuffer);
        }
    }
}
