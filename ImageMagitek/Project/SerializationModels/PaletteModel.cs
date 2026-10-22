using ImageMagitek.Colors;
using System.Collections.Generic;

namespace ImageMagitek.Project.Serialization;

public sealed record PaletteModel : ResourceModel
{
    public ColorModel ColorModel { get; init; }

    /// <summary>
    /// Key to the DataFileSource or null if there's no source (GlobalJson)
    /// </summary>
    public string? DataFileKey { get; init; }
    public bool ZeroIndexTransparent { get; init; }
    public PaletteStorageSource StorageSource { get; init; }

    public IReadOnlyList<IColorSourceModel> ColorSources
    {
        get;
        init => field = EquatableList<IColorSourceModel>.From(value);
    } = EquatableList<IColorSourceModel>.From([]);
}
