using ImageMagitek.Colors;
using TileShop.Shared.Models;

namespace TileShop.UI.Features.Graphics;

/// <summary>
/// Formats hovered colors for the editor's status bar message
/// </summary>
public static class ColorDescription
{
    /// <summary>
    /// Describes a palette color by index, its foreign value (omitted for Rgba32 palettes) and its native RGBA
    /// </summary>
    public static string DescribeIndexed(int x, int y, int index, ColorModel model, IColor foreignColor,
        ColorRgba32 nativeColor, ForeignColorFormat format)
    {
        if (model == ColorModel.Rgba32)
            return $"Color ({x}, {y}): Index {index}, {DescribeNative(nativeColor)}";

        var foreign = format == ForeignColorFormat.Channels ? FormatChannels(foreignColor) : FormatHex(foreignColor);
        return $"Color ({x}, {y}): Index {index}, {Palette.ColorModelToString(model)} {foreign}, {DescribeNative(nativeColor)}";
    }

    public static string DescribeDirect(int x, int y, ColorRgba32 color) => $"Color ({x}, {y}): {DescribeNative(color)}";

    private static string DescribeNative(ColorRgba32 color) => $"rgba({color.R}, {color.G}, {color.B}, {color.A})";

    private static string FormatHex(IColor color) => "0x" + color.Color.ToString($"X{(color.Size + 3) / 4}");

    private static string FormatChannels(IColor color) => color switch
    {
        IColor32 { AlphaMax: > 0 } c => $"({c.R}, {c.G}, {c.B}, {c.A})",
        IColor32 c => $"({c.R}, {c.G}, {c.B})",
        _ => color.Color.ToString(),
    };
}
