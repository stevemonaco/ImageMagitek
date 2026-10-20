using System.Collections.Generic;
using System.Linq;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.Project.Serialization;

public class MetatileMapModel : ResourceModel
{
    public override required string Name { get; init; }
    public required string MapDataKey { get; init; }
    public required long MapOffset { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public MapScreenLayout Layout { get; init; }
    public required string FormatName { get; init; }
    public int EmptyBlock { get; init; }

    /// <summary>
    /// Metatile set keys with their base numbers, in list order
    /// </summary>
    public required IReadOnlyList<(string SetKey, int Base)> Sets { get; init; }

    public override bool ResourceEquals(ResourceModel? resourceModel)
    {
        if (resourceModel is not MetatileMapModel model)
            return false;

        return model.Name == Name && model.MapDataKey == MapDataKey && model.MapOffset == MapOffset &&
            model.Width == Width && model.Height == Height && model.Layout == Layout && model.FormatName == FormatName &&
            model.EmptyBlock == EmptyBlock && model.Sets.SequenceEqual(Sets);
    }
}
