using System;

namespace ImageMagitek.Plugins;

/// <summary>
/// A compression format plugin: a pure transform from a compressed block to its decompressed bytes. The host creates a new
/// instance through a public parameterless constructor for each decompression.
/// </summary>
public interface ICompressionPlugin
{
    /// <summary>
    /// Describes the format. The host reads it once at registration.
    /// </summary>
    CompressionInfo Info { get; }

    /// <summary>
    /// Decompresses the block that starts at the first byte of <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The bytes from the block's start to the end of its data source, capped by the host</param>
    /// <returns>The decompressed bytes and the number of input bytes the block occupies</returns>
    /// <exception cref="System.IO.InvalidDataException">The block is malformed or extends past the end of <paramref name="input"/></exception>
    DecompressionResult Decompress(ReadOnlySpan<byte> input);
}
