using System.Collections.Generic;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.Project.Serialization;

public sealed record MetatileMapModel : ResourceModel
{
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
    public required IReadOnlyList<(string SetKey, int Base)> Sets
    {
        get;
        init => field = EquatableList<(string SetKey, int Base)>.From(value);
    }
}
