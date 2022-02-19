using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.Shared.Models;

public partial class FloodFillAction<TColor> : HistoryAction
    where TColor : struct
{
    public override string Name => "Flood Fill";

    [ObservableProperty] private TColor _fillColor;
    [ObservableProperty] private int _x;
    [ObservableProperty] private int _y;

    public FloodFillAction(int x, int y, TColor fillColor)
    {
        X = x;
        Y = y;
        FillColor = fillColor;
    }
}
