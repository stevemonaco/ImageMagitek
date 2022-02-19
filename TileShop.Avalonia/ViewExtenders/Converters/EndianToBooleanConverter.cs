using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using ImageMagitek;

namespace TileShop.AvaloniaUI.Converters;

public class EndianToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Endian endian)
        {
            if (endian == Endian.Big)
                return true;
            else if (endian == Endian.Little)
                return false;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool endianBool)
        {
            if (endianBool == true)
                return Endian.Big;
            else
                return Endian.Little;
        }

        return AvaloniaProperty.UnsetValue;
    }
}
