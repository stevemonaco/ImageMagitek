using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.Shared.Models;

public partial class FloodFillAction<TColor> : HistoryAction
    where TColor : struct
{
    public override string Name => "Flood Fill";

    [ObservableProperty] private TColor _fillColor;
    [ObservableProperty] private int _x;
    [ObservableProperty] private int _y;

    public Rectangle? ClipBounds { get; }

    public FloodFillAction(int x, int y, TColor fillColor, Rectangle? clipBounds)
    {
        X = x;
        Y = y;
        FillColor = fillColor;
        ClipBounds = clipBounds;
    }
}
