using System.Collections.Generic;
using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.Shared.Utility;

namespace TileShop.Shared.Models;

public partial class PencilHistoryAction<TColor> : HistoryAction
    where TColor : struct
{
    public override string Name => "Pencil";

    [ObservableProperty] private TColor _pencilColor;
    [ObservableProperty] private HashSet<Point> _modifiedPoints = new HashSet<Point>(new PointComparer());

    public PencilHistoryAction(TColor pencilColor)
    {
        _pencilColor = pencilColor;
    }

    public bool Add(double x, double y) => ModifiedPoints.Add(new Point((int)x, (int)y));

    public bool Add(int x, int y) => ModifiedPoints.Add(new Point(x, y));

    public bool Contains(double x, double y) => ModifiedPoints.Contains(new Point((int)x, (int)y));

    public bool Contains(int x, int y) => ModifiedPoints.Contains(new Point(x, y));
}
