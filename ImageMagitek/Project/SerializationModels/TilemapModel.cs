using System.Collections.Generic;
using System.Linq;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.Project.Serialization;

public class TilemapModel : ResourceModel
{
    public override required string Name { get; init; }
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

    /// <summary>
    /// Palette keys by slot index; slots without a palette are absent
    /// </summary>
    public required IReadOnlyDictionary<int, string> PaletteSlots { get; init; }

    public override bool ResourceEquals(ResourceModel? resourceModel)
    {
        if (resourceModel is not TilemapModel model)
            return false;

        return model.Name == Name && model.MapDataKey == MapDataKey && model.MapOffset == MapOffset &&
            model.Width == Width && model.Height == Height && model.Layout == Layout && model.FormatName == FormatName &&
            model.TileDataKey == TileDataKey && model.TileOffset == TileOffset && model.CodecName == CodecName &&
            model.FirstTile == FirstTile && model.PaletteSlots.Count == PaletteSlots.Count &&
            PaletteSlots.All(x => model.PaletteSlots.TryGetValue(x.Key, out var key) && key == x.Value);
    }
}
