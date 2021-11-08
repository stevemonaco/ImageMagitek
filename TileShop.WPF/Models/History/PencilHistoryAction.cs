using System.Collections.Generic;
using System.Drawing;
using TileShop.WPF.Helpers;
using Color = System.Windows.Media.Color;

namespace TileShop.WPF.Models;

public class PencilHistoryAction<TColor> : HistoryAction
    where TColor : struct
{
    public override string Name => "Pencil";

    private TColor _pencilColor;
    public TColor PencilColor
    {
        get => _pencilColor;
        set => SetAndNotify(ref _pencilColor, value);
    }

    private HashSet<Point> _modifiedPoints = new HashSet<Point>(new PointComparer());
    public HashSet<Point> ModifiedPoints
    {
        get => _modifiedPoints;
        set => SetAndNotify(ref _modifiedPoints, value);
    }

    public PencilHistoryAction(TColor pencilColor)
    {
        _pencilColor = pencilColor;
    }

    public bool Add(double x, double y) => ModifiedPoints.Add(new Point((int)x, (int)y));

    public bool Add(int x, int y) => ModifiedPoints.Add(new Point(x, y));

    public bool Contains(double x, double y) => ModifiedPoints.Contains(new Point((int)x, (int)y));

    public bool Contains(int x, int y) => ModifiedPoints.Contains(new Point(x, y));
}
