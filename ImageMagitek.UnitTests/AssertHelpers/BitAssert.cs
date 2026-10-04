using Xunit.Sdk;

namespace ImageMagitek.UnitTests;

public static class BitAssert
{
    /// <summary>
    /// Asserts that every bit outside [startBit, startBit + bitLength) is unchanged, with bits numbered MSB-first.
    /// </summary>
    public static void EqualOutside(byte[] before, byte[] after, long startBit, long bitLength)
    {
        if (before.Length != after.Length)
            throw new XunitException($"Buffer length differs. Expected {before.Length}, actual {after.Length}");

        var endBit = startBit + bitLength;

        for (long bit = 0; bit < before.LongLength * 8; bit++)
        {
            if (bit >= startBit && bit < endBit)
                continue;

            var mask = 0x80 >> (int)(bit % 8);
            if ((before[bit / 8] & mask) != (after[bit / 8] & mask))
                throw new XunitException($"Bit {bit} (byte 0x{bit / 8:X}, bit {bit % 8}) outside [{startBit}, {endBit}) changed. " +
                    $"Before 0x{before[bit / 8]:X2}, after 0x{after[bit / 8]:X2}");
        }
    }
}
