using System;
using System.IO;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek.Codec
{
    public interface IIndexedCodec : IGraphicsCodec<byte> { }

    public abstract class IndexedCodec : IIndexedCodec
    {
        public abstract string Name { get; }
        public abstract int Width { get; }
        public abstract int Height { get; }
        public abstract ImageLayout Layout { get; }
        public PixelColorType ColorType => PixelColorType.Indexed;
        public abstract int ColorDepth { get; }
        public abstract int StorageSize { get; }
        public int RowStride { get; }
        public int ElementStride { get; }

        public virtual ReadOnlySpan<byte> ForeignBuffer => _foreignBuffer;
        protected byte[] _foreignBuffer;

        public virtual byte[,] NativeBuffer => _nativeBuffer;
        protected byte[,] _nativeBuffer;

        public abstract byte[,] DecodeElement(ArrangerElement el, ReadOnlySpan<byte> encodedBuffer);
        public abstract ReadOnlySpan<byte> EncodeElement(ArrangerElement el, byte[,] imageBuffer);

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
