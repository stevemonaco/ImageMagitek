using System;

namespace ImageMagitek.PluginSamples;

/// <summary>
/// MSB-first bit access shared by the sample codecs.
/// </summary>
internal static class SampleBits
{
    public static int ReadBit(ReadOnlySpan<byte> data, int bitIndex) =>
        (data[bitIndex >> 3] >> (7 - (bitIndex & 7))) & 1;

    public static void WriteBit(Span<byte> data, int bitIndex, int bit) =>
        data[bitIndex >> 3] |= (byte)(bit << (7 - (bitIndex & 7)));

    /// <summary>
    /// Reads the 8 bits starting at <paramref name="bitIndex"/>, which need not be byte-aligned.
    /// </summary>
    public static int ReadByte(ReadOnlySpan<byte> data, int bitIndex)
    {
        var result = 0;
        for (int i = 0; i < 8; i++)
            result = (result << 1) | ReadBit(data, bitIndex + i);

        return result;
    }
}
