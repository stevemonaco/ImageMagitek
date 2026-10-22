using System.Collections.Generic;
using ImageMagitek.Colors;

namespace ImageMagitek.Project.Serialization;

/// <summary>
/// A run of scattered colors, stored as one <c>scatteredcolor</c> element
/// </summary>
public sealed record ScatteredColorSourceModel(int Entries, IReadOnlyList<ColorFieldRun> Fields) : IColorSourceModel
{
    public IReadOnlyList<ColorFieldRun> Fields
    {
        get;
        init => field = EquatableList<ColorFieldRun>.From(value);
    } = EquatableList<ColorFieldRun>.From(Fields);
}
