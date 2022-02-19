using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.Shared.Models;

namespace TileShop.AvaloniaUI.Converters;

public static class AppConverters
{
    public static readonly IValueConverter GridlineToStartPoint =
        new FuncValueConverter<Gridline, Point>(line => new Point(line!.X1, line!.Y1));

    public static readonly IValueConverter GridlineToEndPoint =
        new FuncValueConverter<Gridline, Point>(line => new Point(line!.X2, line!.Y2));

    public static readonly IValueConverter PathToGeometry =
        new FuncValueConverter<string, Geometry>(x => Geometry.Parse(x));

    public static readonly IValueConverter ArrangerEditorToWidth =
        new FuncValueConverter<ArrangerEditorViewModel, int>(x => x.WorkingArranger.ArrangerPixelSize.Width);

    public static readonly IValueConverter ArrangerEditorToHeight =
        new FuncValueConverter<ArrangerEditorViewModel, int>(x => x.WorkingArranger.ArrangerPixelSize.Height);
}
