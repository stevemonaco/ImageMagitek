using System;

namespace ImageMagitek.Codec
{
    public interface IGraphicsCodec<T> : IGraphicsCodec where T : struct
    {
        T[,] DecodeElement(ArrangerElement el, ReadOnlySpan<byte> encodedBuffer);
        ReadOnlySpan<byte> EncodeElement(ArrangerElement el, T[,] imageBuffer);

        ReadOnlySpan<byte> ReadElement(ArrangerElement el);
        void WriteElement(ArrangerElement el, ReadOnlySpan<byte> encodedBuffer);

        ReadOnlySpan<byte> ForeignBuffer { get; }
        T[,] NativeBuffer { get; }
    }
}
