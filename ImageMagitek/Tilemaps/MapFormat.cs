using System;
using System.Collections.Generic;

namespace ImageMagitek.Tilemaps;

/// <summary>
/// Declares how one tilemap entry stores its tile index, palette, flip and priority bits
/// </summary>
public sealed class MapFormat
{
    public const int MaxPaletteBits = 4;

    public string Name { get; }
    public int EntryBits { get; }
    public int EntryBytes => EntryBits / 8;
    public Endian Endian { get; }
    public MapField TileIndex { get; }
    public MapField? Palette { get; }
    public MapField? HorizontalFlip { get; }
    public MapField? VerticalFlip { get; }
    public MapField? Priority { get; }

    /// <summary>
    /// The number of palettes an entry can select: one per palette field value, or 1 when the format has no palette field
    /// </summary>
    public int PaletteSlotCount => Palette is { } palette ? 1 << palette.Bits : 1;

    public MapFormat(string name, int entryBits, Endian endian, MapField tileIndex, MapField? palette = null,
        MapField? horizontalFlip = null, MapField? verticalFlip = null, MapField? priority = null)
    {
        Name = name;
        EntryBits = entryBits;
        Endian = endian;
        TileIndex = tileIndex;
        Palette = palette;
        HorizontalFlip = horizontalFlip;
        VerticalFlip = verticalFlip;
        Priority = priority;

        if (FindLayoutError() is { } error)
            throw new ArgumentException(error);
    }

    /// <summary>
    /// The largest tile index the format's tile index field can hold
    /// </summary>
    public int MaxTileIndex => (int)((1L << TileIndex.Bits) - 1);

    /// <summary>
    /// Decodes the entry held in the first <see cref="EntryBytes"/> bytes of <paramref name="entry"/>
    /// </summary>
    public MapEntry Decode(ReadOnlySpan<byte> entry)
    {
        var value = ReadRawValue(entry);

        return new MapEntry(
            TileIndex.Extract(value),
            Palette?.Extract(value) ?? 0,
            HorizontalFlip?.Extract(value) == 1,
            VerticalFlip?.Extract(value) == 1,
            Priority?.Extract(value) == 1);
    }

    /// <summary>
    /// Writes the fields of <paramref name="entry"/> into the entry held in <paramref name="destination"/>, keeping every bit outside the format's fields
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The tile index or palette does not fit its field</exception>
    public void Encode(MapEntry entry, Span<byte> destination)
    {
        if (entry.TileIndex < 0 || entry.TileIndex > MaxTileIndex)
            throw new ArgumentOutOfRangeException(nameof(entry), $"Tile index {entry.TileIndex} does not fit the {TileIndex.Bits}-bit tile field of map format '{Name}'");

        if (entry.Palette < 0 || entry.Palette >= PaletteSlotCount)
            throw new ArgumentOutOfRangeException(nameof(entry), $"Palette {entry.Palette} does not fit the palette field of map format '{Name}'");

        var value = ReadRawValue(destination);
        value = TileIndex.Insert(value, entry.TileIndex);

        if (Palette is { } palette)
            value = palette.Insert(value, entry.Palette);
        if (HorizontalFlip is { } hflip)
            value = hflip.Insert(value, entry.HorizontalFlip ? 1 : 0);
        if (VerticalFlip is { } vflip)
            value = vflip.Insert(value, entry.VerticalFlip ? 1 : 0);
        if (Priority is { } priority)
            value = priority.Insert(value, entry.Priority ? 1 : 0);

        WriteValue(value, destination);
    }

    /// <summary>
    /// The whole value of the entry held in the first <see cref="EntryBytes"/> bytes of <paramref name="entry"/>
    /// </summary>
    public uint ReadRawValue(ReadOnlySpan<byte> entry)
    {
        uint value = 0;
        for (int i = 0; i < EntryBytes; i++)
        {
            var b = Endian == Endian.Little ? entry[i] : entry[EntryBytes - 1 - i];
            value |= (uint)b << (8 * i);
        }

        return value;
    }

    private void WriteValue(uint value, Span<byte> destination)
    {
        for (int i = 0; i < EntryBytes; i++)
        {
            var b = (byte)(value >> (8 * i));
            if (Endian == Endian.Little)
                destination[i] = b;
            else
                destination[EntryBytes - 1 - i] = b;
        }
    }

    private string? FindLayoutError()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return "The map format has no name";

        if (Palette is { Bits: > MaxPaletteBits } wide)
            return $"Map format '{Name}' has a {wide.Bits}-bit palette field; at most {MaxPaletteBits} bits are supported";

        var fields = new List<(string Name, MapField Field)> { ("tileindex", TileIndex) };
        AddIfPresent("palette", Palette);
        AddIfPresent("hflip", HorizontalFlip);
        AddIfPresent("vflip", VerticalFlip);
        AddIfPresent("priority", Priority);

        foreach (var (fieldName, field) in fields)
        {
            if (field.HighBit >= EntryBits)
                return $"Map format '{Name}' field '{fieldName}' (bits {field.LowBit}-{field.HighBit}) does not fit in a {EntryBits}-bit entry";
        }

        for (int i = 0; i < fields.Count; i++)
        {
            for (int j = i + 1; j < fields.Count; j++)
            {
                var (a, b) = (fields[i], fields[j]);
                if (a.Field.LowBit <= b.Field.HighBit && b.Field.LowBit <= a.Field.HighBit)
                    return $"Map format '{Name}' fields '{a.Name}' and '{b.Name}' overlap";
            }
        }

        return null;

        void AddIfPresent(string fieldName, MapField? field)
        {
            if (field is { } value)
                fields.Add((fieldName, value));
        }
    }
}
