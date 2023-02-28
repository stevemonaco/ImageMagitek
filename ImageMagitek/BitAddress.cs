using System;

namespace ImageMagitek;

/// <summary>
/// Struct used to store an address that does not necessarily start on a byte-aligned address
/// </summary>
public readonly struct BitAddress : IEquatable<BitAddress>
{
    /// <summary>
    /// Portion of offset in bytes
    /// </summary>
    public long ByteOffset { get; }

    /// <summary>
    /// Portion of offset in bits following ByteOffset
    /// Valid range is 0-7 inclusive
    /// A zero value would result in a byte-aligned address
    /// </summary>
    public int BitOffset { get; }

    /// <summary>
    /// Full offset in number of bits
    /// </summary>
    public long Offset => ByteOffset * 8 + BitOffset;

    public BitAddress(long byteOffset, int bitOffset)
    {
        if (bitOffset > 7 || bitOffset < 0)
            throw new ArgumentOutOfRangeException($"{nameof(BitAddress)}: {nameof(bitOffset)} '{bitOffset}' is out of range");

        ByteOffset = byteOffset;
        BitOffset = bitOffset;
    }

    /// <summary>
    /// Construct a new BitAddress from the number of bits to the address
    /// </summary>
    /// <param name="bits"></param>
    public BitAddress(long bits)
    {
        ByteOffset = bits / 8;
        BitOffset = (int)(bits % 8);
    }

    public bool Equals(BitAddress other) => ByteOffset == other.ByteOffset && BitOffset == other.BitOffset;

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        return Equals((BitAddress)obj);
    }

    public override int GetHashCode() => HashCode.Combine(BitOffset, ByteOffset);

    public static BitAddress Zero => new(0, 0);

    public static bool operator ==(BitAddress lhs, BitAddress rhs) => lhs.Equals(rhs);

    public static bool operator !=(BitAddress lhs, BitAddress rhs) => !lhs.Equals(rhs);

    /// <summary>
    /// Adds two BitAddress objects and returns the result
    /// </summary>
    /// <param name="lhs"></param>
    /// <param name="rhs"></param>
    /// <returns></returns>
    public static BitAddress operator +(BitAddress lhs, BitAddress rhs) => new(lhs.Offset + rhs.Offset);

    /// <summary>
    /// Adds a specified number of bits to a BitAddress object
    /// </summary>
    /// <param name="lhs"></param>
    /// <param name="offset">Number of bits to advance the address</param>
    /// <returns></returns>
    public static BitAddress operator +(BitAddress lhs, long offset) => new(lhs.Offset + offset);

    /// <summary>
    /// Subtracts two BitAddress objects and returns the result
    /// </summary>
    /// <param name="lhs"></param>
    /// <param name="rhs"></param>
    /// <returns></returns>
    public static BitAddress operator -(BitAddress lhs, BitAddress rhs) => new(lhs.Offset - rhs.Offset);

    /// <summary>
    /// Subtracts a number of bits from a BitAddress object
    /// </summary>
    /// <param name="lhs"></param>
    /// <param name="offset">Offset in number of bits</param>
    /// <returns></returns>
    public static BitAddress operator -(BitAddress lhs, long offset) => new(lhs.Offset - offset);

    public static bool operator <(BitAddress lhs, BitAddress rhs) => lhs.Offset < rhs.Offset;

    public static bool operator <=(BitAddress lhs, BitAddress rhs) => lhs.Offset <= rhs.Offset;

    public static bool operator >(BitAddress lhs, BitAddress rhs) => lhs.Offset > rhs.Offset;

    public static bool operator >=(BitAddress lhs, BitAddress rhs) => lhs.Offset >= rhs.Offset;
}
