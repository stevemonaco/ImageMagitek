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
        if (value is NumericBase numericBase)
        {
            if (numericBase == NumericBase.Decimal)
                return false;
            else if (numericBase == NumericBase.Hexadecimal)
                return true;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool baseBool)
        {
            if (baseBool == false)
                return NumericBase.Decimal;
            else
                return NumericBase.Hexadecimal;
        }

        return AvaloniaProperty.UnsetValue;
    }
}
