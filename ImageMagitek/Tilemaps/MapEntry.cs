namespace ImageMagitek.Tilemaps;

/// <summary>
/// The fields of one decoded tilemap entry
/// </summary>
public readonly record struct MapEntry(int TileIndex, int Palette, bool HorizontalFlip, bool VerticalFlip, bool Priority)
{
    public MirrorOperation Mirror => (HorizontalFlip, VerticalFlip) switch
    {
        (true, true) => MirrorOperation.Both,
        (true, false) => MirrorOperation.Horizontal,
        (false, true) => MirrorOperation.Vertical,
        _ => MirrorOperation.None
    };
}

/// <summary>
/// A run of bits within a map entry, counted from the entry's least significant bit
/// </summary>
public readonly record struct MapField(int LowBit, int Bits)
{
    public int HighBit => LowBit + Bits - 1;

    private uint Mask => (uint)((1UL << Bits) - 1);

    public int Extract(uint entry) => (int)((entry >> LowBit) & Mask);

    /// <summary>
    /// Returns <paramref name="entry"/> with this field's bits replaced by <paramref name="value"/>
    /// </summary>
    public uint Insert(uint entry, int value) => (entry & ~(Mask << LowBit)) | (((uint)value & Mask) << LowBit);

    /// <summary>
    /// Returns <paramref name="entry"/> without this field's bits, the bits above it shifted down to close the gap
    /// </summary>
    public uint Remove(uint entry)
    {
        var low = entry & (uint)((1UL << LowBit) - 1);
        var high = (uint)((ulong)entry >> (LowBit + Bits));
        return low | (uint)((ulong)high << LowBit);
    }
}
