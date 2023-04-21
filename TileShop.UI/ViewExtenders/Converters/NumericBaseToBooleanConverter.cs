using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Converters;
public class NumericBaseToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            NumericBase.Hexadecimal => true,
            NumericBase.Decimal => false,
            _ => AvaloniaProperty.UnsetValue
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            true => NumericBase.Hexadecimal,
            false => NumericBase.Decimal,
            _ => AvaloniaProperty.UnsetValue
        };
    }
}
