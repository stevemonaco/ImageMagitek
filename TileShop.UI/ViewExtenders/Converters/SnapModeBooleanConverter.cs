using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using TileShop.Shared.Models;

namespace TileShop.UI.Converters;

public class SnapModeBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            SnapMode.Element => true,
            SnapMode.Pixel => false,
            _ => AvaloniaProperty.UnsetValue
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            true => SnapMode.Element,
            false => SnapMode.Pixel,
            _ => AvaloniaProperty.UnsetValue
        };
    }
}
