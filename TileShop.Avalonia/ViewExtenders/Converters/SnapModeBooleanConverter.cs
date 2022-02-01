using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using TileShop.Shared.Models;

namespace TileShop.AvaloniaUI.Converters;

public class SnapModeBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SnapMode mode)
        {
            if (mode == SnapMode.Element)
                return true;
            else if (mode == SnapMode.Pixel)
                return false;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool snapBool)
        {
            if (snapBool == true)
                return SnapMode.Element;
            else
                return SnapMode.Pixel;
        }

        return AvaloniaProperty.UnsetValue;
    }
}
