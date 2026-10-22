using System.Collections.Generic;
using ImageMagitek.Metatiles;

namespace ImageMagitek.Project.Serialization;

public sealed record MetatileSetModel : ResourceModel
{
    public required string MetatileDataKey { get; init; }
    public required long MetatileOffset { get; init; }
    public required int MetatileCount { get; init; }
    public int MetatileWidth { get; init; }
    public int MetatileHeight { get; init; }
    public int Layers { get; init; }
    public required string FormatName { get; init; }
    public MetatileOrder Order { get; init; }
    public required string TileDataKey { get; init; }
    public required long TileOffset { get; init; }
    public required string CodecName { get; init; }
    public int FirstTile { get; init; }
    public int? TileCount { get; init; }

    /// <summary>
    /// Palette keys by slot index; slots without a palette are absent
    /// </summary>
    public required IReadOnlyDictionary<int, string> PaletteSlots
    {
        get;
        init => field = EquatableDictionary<int, string>.From(value);
    }

    /// <summary>
    /// Key of the attribute table's data file, or null when the set has no attribute table
    /// </summary>
    public string? AttributeDataKey { get; init; }
    public long AttributeOffset { get; init; }
    public int? AttributeSize { get; init; }
}
