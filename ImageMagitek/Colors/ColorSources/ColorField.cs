namespace ImageMagitek.Colors;

/// <summary>
/// Bits of one byte of a data file that a scattered color places into its raw value
/// </summary>
/// <param name="Offset">Byte offset within the data file</param>
/// <param name="LowBit">Lowest bit read, numbered from the least significant bit of the byte</param>
/// <param name="Bits">Number of bits read</param>
/// <param name="Shift">Bit position in the raw value where the field's lowest bit lands</param>
public readonly record struct ColorField(long Offset, int LowBit, int Bits, int Shift)
{
    public uint ValueMask => (uint)(((1ul << Bits) - 1) << Shift);
    public long BitPosition => Offset * 8 + LowBit;
}

/// <summary>
/// A field's position for the first entry of a scattered run plus how far it advances, in bits, for each following entry
/// </summary>
public readonly record struct ColorFieldRun(ColorField First, int Stride);
