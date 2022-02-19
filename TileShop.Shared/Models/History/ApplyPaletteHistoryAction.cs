using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Colors;
using System.Collections.Generic;
using System.Drawing;
using TileShop.Shared.Utility;

namespace TileShop.Shared.Models;

public partial class ApplyPaletteHistoryAction : HistoryAction
{
    public override string Name => "Apply Palette";

    public Palette Palette { get; }

    [ObservableProperty] private HashSet<Point> _modifiedElements = new HashSet<Point>(new PointComparer());

    public ApplyPaletteHistoryAction(Palette palette)
    {
        Palette = palette;
    }

    /// <summary>
    /// Adds a location that a palette was applied to
    /// </summary>
    /// <param name="x">x-coordinate in pixel coordinates</param>
    /// <param name="y">y-coordinate in pixel coordinates</param>
    /// <returns></returns>
    public bool Add(int x, int y) => ModifiedElements.Add(new Point(x, y));

    public bool Contains(int x, int y) => ModifiedElements.Contains(new Point(x, y));
}
