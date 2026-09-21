using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using TileShop.Shared.Models;
using TileShop.UI.Controls;
using TileShop.UI.Features.Graphics;
using TileShop.UI.Models;
using TileShop.UI.ViewModels;

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

    public static IValueConverter NumericBaseToPrefix { get; } =
        new FuncValueConverter<NumericBase, string?>(x => x == NumericBase.Hexadecimal ? "0x" : null);

    public static IValueConverter EditModeToScrollBarVisibility { get; } =
        new FuncValueConverter<GraphicsEditMode, ScrollBarVisibility>(x =>
            x == GraphicsEditMode.View ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden);

    public static IValueConverter ZoomToInverted { get; } =
        new FuncValueConverter<double, double>(x => 1 / x);

    public static IValueConverter ColorToBrush { get; } =
        new FuncValueConverter<Color, IBrush>(x => new ImmutableSolidColorBrush(x));

    public static ColorRgba32ToMediaColorConverter ColorRgba32ToMediaColor { get; } = new();
    public static EndianToBooleanConverter EndianToBoolean { get; } = new();
    public static EnumToBoolConverter EnumToBoolean { get; } = new();
    public static LongToHexadecimalConverter LongToHexadecimal { get; } = new();
    public static NumericBaseToBooleanConverter NumericBaseToBoolean { get; } = new();
    public static PaletteEntryToIndexConverter PaletteEntryToIndex { get; } = new();
    public static PaletteCountToSwatchSizeConverter PaletteCountToSwatchSize { get; } = new();
    public static PaletteModelIndexToSolidColorBrushConverter PaletteIndexToBrush { get; } = new();
    public static SnapModeBooleanConverter SnapModeBoolean { get; } = new();
    public static SwatchSelectionBorderConverter SwatchSelectionBorder { get; } = new();

    public static IValueConverter ResourceToTabIcon { get; } =
        new FuncValueConverter<IProjectResource, IImage?>(resource => resource switch
        {
            Palette => AppIcons.NewNodePalette,
            SequentialArranger => AppIcons.NewNodeFile,
            ScatteredArranger => AppIcons.NewNodeArranger,
            DataSource => AppIcons.NewNodeFile,
            ImageProject => AppIcons.NewNodeProject,
            ResourceFolder => AppIcons.NewNodeFolder,
            _ => null,
        });
}
