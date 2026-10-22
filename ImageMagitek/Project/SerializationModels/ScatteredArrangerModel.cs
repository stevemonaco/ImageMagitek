using System;
using System.Drawing;
using System.Linq;

namespace ImageMagitek.Project.Serialization;

/// <summary>
/// A scattered arranger's model; equality compares the layout and every element but not <see cref="ResourceModel.Name"/>
/// </summary>
public sealed record ScatteredArrangerModel : ResourceModel
{
    public required ArrangerElementModel[,] ElementGrid { get; init; }
    public Size ArrangerElementSize { get; init; }
    public Size ElementPixelSize { get; init; }
    public ElementLayout Layout { get; init; }
    public PixelColorType ColorType { get; init; }

    public bool Equals(ScatteredArrangerModel? other)
    {
        if (other is null)
            return false;

        if (other.ArrangerElementSize != ArrangerElementSize || other.ElementPixelSize != ElementPixelSize ||
            other.Layout != Layout || other.ColorType != ColorType)
            return false;

        return other.EnumerateElements()
            .Zip(this.EnumerateElements())
            .All(x => Equals(x.First, x.Second));
    }

    public override int GetHashCode() => HashCode.Combine(ArrangerElementSize, ElementPixelSize, Layout, ColorType);
}
