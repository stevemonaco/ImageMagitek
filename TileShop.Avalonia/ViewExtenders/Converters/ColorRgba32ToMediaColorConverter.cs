using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using ImageMagitek.Colors;
using MediaColor = Avalonia.Media.Color;

namespace TileShop.AvaloniaUI.Converters;
public class ColorRgba32ToMediaColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ColorRgba32 color)
        {
            return new MediaColor(color.A, color.R, color.G, color.B);
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MediaColor color)
            return new ColorRgba32(color.R, color.G, color.B, color.A);

        return AvaloniaProperty.UnsetValue;
    }
}
