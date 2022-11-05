using System;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
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

    public static readonly IValueConverter PaletteEntryToSolidColorBrush =
        new FuncValueConverter<PaletteEntry, SolidColorBrush>(p => new SolidColorBrush(p.Color));

    public static readonly IValueConverter PluralCountToBoolean =
        new FuncValueConverter<int, bool>(x => x >= 2);

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

    public static readonly ColorRgba32ToMediaColorConverter ColorRgba32ToMediaColor = new();
    public static readonly EndianToBooleanConverter EndianToBoolean = new();
    public static readonly EnumToBooleanConverter EnumToBoolean = new();
    public static readonly LongToHexadecimalConverter LongToHexadecimal = new();
    public static readonly NumericBaseToBooleanConverter NumericBaseToBoolean = new();
    public static readonly PaletteEntryToIndexConverter PaletteEntryToIndex = new();
    public static readonly PaletteModelIndexToSolidColorBrushConverter PaletteIndexToBrush = new();
    public static readonly SnapModeBooleanConverter SnapModeBoolean = new();
}
