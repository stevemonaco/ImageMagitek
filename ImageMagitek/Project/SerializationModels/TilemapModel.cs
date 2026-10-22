using System.Collections.Generic;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.Project.Serialization;

public sealed record TilemapModel : ResourceModel
{
    public required string MapDataKey { get; init; }
    public required long MapOffset { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public MapScreenLayout Layout { get; init; }
    public required string FormatName { get; init; }
    public required string TileDataKey { get; init; }
    public required long TileOffset { get; init; }
    public required string CodecName { get; init; }
    public int FirstTile { get; init; }
    public int EmptyTile { get; init; }
    public int? TileCount { get; init; }

    /// <summary>
    /// Key of the scattered arranger used as the tile palette, or null to generate one from the tile set
    /// </summary>
    public string? TilePaletteKey { get; init; }

    /// <summary>
    /// Palette keys by slot index; slots without a palette are absent
    /// </summary>
    public required IReadOnlyDictionary<int, string> PaletteSlots
    {
        get;
        init => field = EquatableDictionary<int, string>.From(value);
    }
}
