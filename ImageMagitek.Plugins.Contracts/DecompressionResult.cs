namespace ImageMagitek.Plugins;

/// <summary>
/// The result of decompressing one block.
/// </summary>
public sealed class DecompressionResult
{
    /// <summary>
    /// The decompressed bytes.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// The number of input bytes the compressed block occupies.
    /// </summary>
    public int ConsumedLength { get; }

    public DecompressionResult(byte[] data, int consumedLength)
    {
        Data = data;
        ConsumedLength = consumedLength;
    }
}
