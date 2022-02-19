using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace TileShop.AvaloniaUI.Converters;

public class LongToHexadecimalConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return null;

        if (value is long number)
            return $"{number:X}";

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return AvaloniaProperty.UnsetValue;

        if (value is string hexString)
        {
            if (long.TryParse(hexString, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out long number))
            {
                return number;
            }
        }

        return AvaloniaProperty.UnsetValue;
    }
}
