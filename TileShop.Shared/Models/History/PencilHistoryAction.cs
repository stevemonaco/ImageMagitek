using System.Collections.Generic;
using System.Drawing;
using TileShop.Shared.Utility;

namespace TileShop.Shared.Models;

/// <summary>
/// A Pencil stroke, recorded as the index or color written at each pixel
/// </summary>
public sealed class PencilHistoryAction<TColor> : HistoryAction
    where TColor : struct
{
    public override string Name => "Pencil";

    public Dictionary<Point, TColor> ModifiedPoints { get; } = new(new PointComparer());

    /// <returns>True when the point was not already part of the stroke</returns>
    public bool Add(int x, int y, TColor written) => ModifiedPoints.TryAdd(new Point(x, y), written);
}
