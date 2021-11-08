using System;

namespace ImageMagitek.Codec;

public interface IGraphicsCodec<T> : IGraphicsCodec where T : struct
{
    /// <summary>
    /// Decodes an element from foreign pixel data
    /// </summary>
    /// <param name="el">Element to decode</param>
    /// <param name="encodedBuffer">Buffer containing foreign pixel data for the element</param>
    /// <returns>Native array with [y, x] ordering</returns>
    T[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer);

    /// <summary>
    /// Encodes an element from native pixel data
    /// </summary>
    /// <param name="el">Element to encode</param>
    /// <param name="imageBuffer">Native pixel data with [y, x] ordering</param>
    /// <returns></returns>
    ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, T[,] imageBuffer);

    /// <summary>
    /// Reads an element's foreign pixel data
    /// </summary>
    /// <param name="el">Element to read</param>
    /// <returns></returns>
    ReadOnlySpan<byte> ReadElement(in ArrangerElement el);

    /// <summary>
    /// Writes an element's foreign pixel data
    /// </summary>
    /// <param name="el">Element to write</param>
    /// <param name="encodedBuffer">Foreign pixel data</param>
    void WriteElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer);

    /// <summary>
    /// Contains foreign pixel data for the element
    /// </summary>
    ReadOnlySpan<byte> ForeignBuffer { get; }

    /// <summary>
    /// Contains native pixel data for the element in [y, x] order
    /// </summary>
    T[,] NativeBuffer { get; }
}
