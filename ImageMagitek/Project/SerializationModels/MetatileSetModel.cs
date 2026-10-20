using System.Collections.Generic;
using System.Linq;
using ImageMagitek.Metatiles;

namespace ImageMagitek.Project.Serialization;

public class MetatileSetModel : ResourceModel
{
    public override required string Name { get; init; }
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
    public required IReadOnlyDictionary<int, string> PaletteSlots { get; init; }

    /// <summary>
    /// Key of the attribute table's data file, or null when the set has no attribute table
    /// </summary>
    public string? AttributeDataKey { get; init; }
    public long AttributeOffset { get; init; }
    public int? AttributeSize { get; init; }

    public override bool ResourceEquals(ResourceModel? resourceModel)
    {
        if (resourceModel is not MetatileSetModel model)
            return false;

        return model.Name == Name && model.MetatileDataKey == MetatileDataKey && model.MetatileOffset == MetatileOffset &&
            model.MetatileCount == MetatileCount && model.MetatileWidth == MetatileWidth && model.MetatileHeight == MetatileHeight &&
            model.Layers == Layers && model.FormatName == FormatName && model.Order == Order &&
            model.TileDataKey == TileDataKey && model.TileOffset == TileOffset && model.CodecName == CodecName &&
            model.FirstTile == FirstTile && model.TileCount == TileCount &&
            model.AttributeDataKey == AttributeDataKey && model.AttributeOffset == AttributeOffset && model.AttributeSize == AttributeSize &&
            model.PaletteSlots.Count == PaletteSlots.Count &&
            PaletteSlots.All(x => model.PaletteSlots.TryGetValue(x.Key, out var key) && key == x.Value);
    }
}
