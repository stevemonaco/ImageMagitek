using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.Metatiles;

/// <summary>
/// A table of metatiles, each a fixed rectangle of map entries in one or more layers, with the tile set and palette slots they draw from.
/// </summary>
public sealed class MetatileSet : IProjectResource
{
    public const int MaxShapeSize = 8;
    public const int MaxLayers = 4;
    public const int MaxAttributeSize = 8;

    public string Name { get; set; }
    public bool CanContainChildResources => false;
    public bool ShouldBeSerialized { get; set; } = true;

    public DataSource MetatileSource { get; }
    public long MetatileOffset { get; }

    /// <summary>
    /// The declared number of metatiles in the table
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// The size of one metatile in tiles
    /// </summary>
    public Size Shape { get; }

    public int Layers { get; }
    public MapFormat EntryFormat { get; }
    public MetatileOrder Order { get; }
    public TileSet TileSet { get; }

    /// <summary>
    /// The palette for each value of the entry's palette field, padded to the format's slot count; an empty slot uses slot 0
    /// </summary>
    public IReadOnlyList<Palette?> PaletteSlots { get; }

    public DataSource? AttributeSource { get; }
    public long AttributeOffset { get; }

    /// <summary>
    /// The bytes per metatile of the attribute table, or 0 when the set has none
    /// </summary>
    public int AttributeSize { get; }

    /// <exception cref="ArgumentException">A value is out of range, or the tile set's codec is unknown or not indexed; the message names the set</exception>
    public MetatileSet(string name, DataSource metatileSource, long metatileOffset, int count, Size shape, int layers, MapFormat entryFormat,
        MetatileOrder order, DataSource tileSource, long tileOffset, string codecName, int firstTile, int? tileCount,
        IReadOnlyList<Palette?> paletteSlots, ICodecFactory codecFactory,
        DataSource? attributeSource = null, long attributeOffset = 0, int attributeSize = 0)
    {
        if (count <= 0)
            throw new ArgumentException($"Metatile set '{name}' must declare a positive metatile count, not {count}");

        if (shape.Width is < 1 or > MaxShapeSize || shape.Height is < 1 or > MaxShapeSize)
            throw new ArgumentException($"Metatile set '{name}' has metatile shape {shape.Width}x{shape.Height}; width and height must be 1 to {MaxShapeSize} tiles");

        if (layers is < 1 or > MaxLayers)
            throw new ArgumentException($"Metatile set '{name}' has {layers} layers; it must have 1 to {MaxLayers}");

        if (attributeSource is not null && attributeSize is < 1 or > MaxAttributeSize)
            throw new ArgumentException($"Metatile set '{name}' has attribute size {attributeSize}; it must be 1 to {MaxAttributeSize} bytes");

        try
        {
            TileSet = new TileSet(tileSource, tileOffset, codecName, firstTile, tileCount, codecFactory);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"Metatile set '{name}' {ex.Message}");
        }

        Name = name;
        MetatileSource = metatileSource;
        MetatileOffset = metatileOffset;
        Count = count;
        Shape = shape;
        Layers = layers;
        EntryFormat = entryFormat;
        Order = order;
        AttributeSource = attributeSource;
        AttributeOffset = attributeSource is null ? 0 : attributeOffset;
        AttributeSize = attributeSource is null ? 0 : attributeSize;

        var slots = new Palette?[entryFormat.PaletteSlotCount];
        for (int i = 0; i < Math.Min(slots.Length, paletteSlots.Count); i++)
            slots[i] = paletteSlots[i];
        PaletteSlots = slots;
    }

    private long EntryCount => (long)Count * Layers * Shape.Width * Shape.Height;

    private long EntryIndex(int metatile, int layer, int x, int y) =>
        Order.EntryIndex(metatile, layer, x, y, Shape.Width, Shape.Height, Layers, Count);

    /// <summary>
    /// Reads the whole metatile table, clamped to the end of the source, for <see cref="TryDecodeEntry"/>
    /// </summary>
    public byte[] ReadTable() => MapDataReader.Read(MetatileSource, MetatileOffset, EntryCount, EntryFormat.EntryBytes).Data;

    /// <summary>
    /// Decodes an entry from <paramref name="table"/>, as read by <see cref="ReadTable"/>
    /// </summary>
    /// <returns>False when the entry lies past the end of the table data</returns>
    public bool TryDecodeEntry(ReadOnlySpan<byte> table, int metatile, int layer, int x, int y, out MapEntry entry)
    {
        var entryBytes = EntryFormat.EntryBytes;
        var offset = EntryIndex(metatile, layer, x, y) * entryBytes;

        if (offset < 0 || offset + entryBytes > table.Length)
        {
            entry = default;
            return false;
        }

        entry = EntryFormat.Decode(table.Slice((int)offset, entryBytes));
        return true;
    }

    /// <summary>
    /// The metatile's attribute as an unsigned little-endian integer, or null when the set has no attribute table or the bytes lie past its source
    /// </summary>
    public ulong? ReadAttribute(int metatile)
    {
        if (AttributeSource is not { } source || metatile < 0 || metatile >= Count)
            return null;

        var data = MapDataReader.Read(source, AttributeOffset + (long)metatile * AttributeSize, 1, AttributeSize);
        if (data.ReadEntries < 1)
            return null;

        ulong value = 0;
        for (int i = 0; i < AttributeSize; i++)
            value |= (ulong)data.Data[i] << (8 * i);

        return value;
    }

    /// <summary>
    /// The metatile, tile and attribute sources and each slot palette's source
    /// </summary>
    internal IEnumerable<DataSource> ReadSources =>
        new[] { MetatileSource, TileSet.Source, AttributeSource }
            .Concat(PaletteSlots.OfType<Palette>().Select(x => x.DataSource))
            .OfType<DataSource>();

    /// <summary>
    /// The first missing file the set reads, directly or through compressed data or a slot palette, or null when none is missing
    /// </summary>
    public FileDataSource? FindMissingDataSource() => FindMissing(ReadSources);

    internal static FileDataSource? FindMissing(IEnumerable<DataSource> sources) =>
        sources.Select(x => x is CompressedDataSource compressed ? compressed.Parent : x)
            .OfType<FileDataSource>()
            .Distinct()
            .FirstOrDefault(x => x.IsMissing);

    /// <summary>
    /// The metatile, tile and attribute sources and every assigned palette slot
    /// </summary>
    public IEnumerable<IProjectResource> LinkedResources =>
        new IProjectResource?[] { MetatileSource, TileSet.Source, AttributeSource }
            .OfType<IProjectResource>()
            .Concat(PaletteSlots.OfType<Palette>())
            .Distinct()
            .ToList();

    public bool UnlinkResource(IProjectResource resource) => false;
}
