using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace TileShop.UI.Converters;

public class PaletteCountToSwatchSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count switch
            {
                <= 16 => 38.0,
                _ => 18.0
            };
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
