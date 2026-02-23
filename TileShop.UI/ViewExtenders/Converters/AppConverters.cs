using System;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TileShop.UI.Models;
using TileShop.UI.ViewModels;
using TileShop.Shared.Models;
using TileShop.UI.Features.Graphics;

namespace TileShop.UI.Converters;

public static class AppConverters
{
    public static IValueConverter GridlineToStartPoint { get; } =
        new FuncValueConverter<Gridline, Point>(line => line is not null ? new Point(line!.X1, line!.Y1) : new Point(0, 0));

    public static IValueConverter GridlineToEndPoint { get; } =
        new FuncValueConverter<Gridline, Point>(line => line is not null ? new Point(line!.X2, line!.Y2) : new Point(0, 0));

    public static IValueConverter PathToGeometry { get; } =
        new FuncValueConverter<string, Geometry?>(x => x is not null ? Geometry.Parse(x) : null);

    public static IValueConverter ArrangerEditorToWidth { get; } =
        new FuncValueConverter<GraphicsEditorViewModel, int>(x => x!.WorkingArranger.ArrangerPixelSize.Width);

    public static IValueConverter ArrangerEditorToHeight { get; } =
        new FuncValueConverter<GraphicsEditorViewModel, int>(x => x!.WorkingArranger.ArrangerPixelSize.Height);

    public static IValueConverter PaletteEntryToSolidColorBrush { get; } =
        new FuncValueConverter<PaletteEntry, SolidColorBrush>(p => new SolidColorBrush(p?.Color ?? Colors.Transparent));

    public static IValueConverter PluralCountToBoolean { get; } =
        new FuncValueConverter<int, bool>(x => x >= 2);

    public static IValueConverter NumericBaseToString { get; } =
        new FuncValueConverter<NumericBase, string>(x =>
        {
            return x switch
            {
                NumericBase.Decimal => "Dec",
                NumericBase.Hexadecimal => "Hex",
                _ => throw new InvalidOperationException($"{nameof(NumericBaseToString)} cannot convert from given type {x.GetType()} with value {x}"),
            };
        });

    public static IValueConverter ZoomToInverted { get; } =
        new FuncValueConverter<double, double>(x => 1 / x);

    public static IValueConverter ColorToBrush { get; } =
        new FuncValueConverter<Color, IBrush>(x => new ImmutableSolidColorBrush(x));

    public static ColorRgba32ToMediaColorConverter ColorRgba32ToMediaColor { get; } = new();
    public static EndianToBooleanConverter EndianToBoolean { get; } = new();
    public static EnumToBooleanConverter EnumToBoolean { get; } = new();
    public static LongToHexadecimalConverter LongToHexadecimal { get; } = new();
    public static NumericBaseToBooleanConverter NumericBaseToBoolean { get; } = new();
    public static PaletteEntryToIndexConverter PaletteEntryToIndex { get; } = new();
    public static PaletteModelIndexToSolidColorBrushConverter PaletteIndexToBrush { get; } = new();
    public static SnapModeBooleanConverter SnapModeBoolean { get; } = new();
}
