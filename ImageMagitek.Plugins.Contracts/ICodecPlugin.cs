namespace ImageMagitek.Plugins;

/// <summary>
/// A codec plugin: a pure transform between encoded bytes and pixels. The host creates one instance per codec
/// through a public parameterless constructor and never calls one instance from two threads at once.
/// </summary>
/// <remarks>
/// Pixels are row-major (<c>y * width + x</c>). The encoded buffer is <c>(GetStorageBits(width, height) + 7) / 8</c> bytes;
/// the host zeroes it before Encode and writes back only the storage bits.
/// </remarks>
public interface ICodecPlugin
{
    /// <summary>
    /// Describes the codec. The host reads it once at registration and once per created codec.
    /// </summary>
    CodecInfo Info { get; }

    /// <summary>
    /// Returns the number of encoded bits for an element of the given size.
    /// </summary>
    int GetStorageBits(int width, int height);
}
