using System;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ImageMagitek.Colors;
using TileShop.AvaloniaUI.Models;
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

    public static readonly IValueConverter ColorRgba32ToMediaColor =
        new FuncValueConverter<ColorRgba32, Color>(c => new Color(c.A, c.R, c.G, c.B));

    public static readonly IValueConverter PaletteEntryToSolidColorBrush =
        new FuncValueConverter<PaletteEntry, SolidColorBrush>(p => new SolidColorBrush(p.Color));

    public static readonly IValueConverter NumericBaseToString =
        new FuncValueConverter<NumericBase, string>(x =>
        {
            return x switch
            {
                NumericBase.Decimal => "Dec",
                NumericBase.Hexadecimal => "Hex",
                _ => throw new InvalidOperationException($"{nameof(NumericBaseToString)} cannot convert from given type {x.GetType()} with value {x}"),
            };
        });
}
