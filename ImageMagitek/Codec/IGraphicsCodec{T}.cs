using System;

namespace ImageMagitek.Codec
{
    public interface IGraphicsCodec<T> : IGraphicsCodec where T : struct
    {
        T[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer);
        ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, T[,] imageBuffer);

        ReadOnlySpan<byte> ReadElement(in ArrangerElement el);
        void WriteElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer);

        ReadOnlySpan<byte> ForeignBuffer { get; }
        T[,] NativeBuffer { get; }
    }
}
