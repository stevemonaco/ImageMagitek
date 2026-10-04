using System;

namespace ImageMagitek.PluginSample;

/// <summary>
/// MSB-first bit access shared by the sample planar codecs.
/// </summary>
internal static class SampleBits
{
    public static int ReadBit(ReadOnlySpan<byte> data, int bitIndex) =>
        (data[bitIndex >> 3] >> (7 - (bitIndex & 7))) & 1;

    public static void WriteBit(Span<byte> data, int bitIndex, int bit) =>
        data[bitIndex >> 3] |= (byte)(bit << (7 - (bitIndex & 7)));
}