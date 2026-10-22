using System;
using ImageMagitek.Colors;

namespace ImageMagitek.Project.Serialization;

/// <summary>
/// A foreign color; equality compares the color's type and packed value
/// </summary>
public sealed record ProjectForeignColorSourceModel(IColor Value) : IColorSourceModel
{
    public bool Equals(ProjectForeignColorSourceModel? other) =>
        other is not null && other.Value.GetType() == Value.GetType() && other.Value.Color == Value.Color;

    public override int GetHashCode() => HashCode.Combine(Value.GetType(), Value.Color);
}
