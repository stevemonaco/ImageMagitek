using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using ImageMagitek.Colors;
using MediaColor = Avalonia.Media.Color;

namespace TileShop.UI.Converters;
public class ColorRgba32ToMediaColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ColorRgba32 color)
        {
            return ToMediaColor(color);
        }

        return AvaloniaProperty.UnsetValue;
    }

    public static MediaColor ToMediaColor(ColorRgba32 color) => new(color.A, color.R, color.G, color.B);

    /// <summary>
    /// Formats as #RRGGBB, or #RRGGBBAA when not fully opaque
    /// </summary>
    public static string ToHex(ColorRgba32 color) =>
        color.A == 255 ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MediaColor color)
            return new ColorRgba32(color.R, color.G, color.B, color.A);

        return AvaloniaProperty.UnsetValue;
    }
}
