using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ImageMagitek.Codec;

/// <summary>
/// MSB-first bit access matching <see cref="BitStream"/>, for the generalized codecs' hot loops.
/// </summary>
internal static class PackedBits
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadBit(ReadOnlySpan<byte> data, int bitIndex) =>
        (data[bitIndex >> 3] >> (7 - (bitIndex & 7))) & 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetBit(Span<byte> data, int bitIndex) =>
        data[bitIndex >> 3] |= (byte)(0x80 >> (bitIndex & 7));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> AsFlatSpan(byte[,] array) =>
        MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(array), array.Length);
}
