using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using ImageMagitek;
using TileShop.Shared.Models;

namespace TileShop.UI.Converters;

public class EndianToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            Endian.Big => true,
            Endian.Little => false,
            _ => AvaloniaProperty.UnsetValue
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            true => Endian.Big,
            false => Endian.Little,
            _ => AvaloniaProperty.UnsetValue
        };
    }
}
