using System;
using System.Globalization;
using System.Windows.Data;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF.Converters;

public class NumericBaseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NumericBase numericBase)
        {
            if (numericBase == NumericBase.Decimal)
                return false;
            else if (numericBase == NumericBase.Hexadecimal)
                return true;
        }

        return Binding.DoNothing;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool baseBool)
        {
            if (baseBool == false)
                return NumericBase.Decimal;
            else
                return NumericBase.Hexadecimal;
        }

        return Binding.DoNothing;
    }
}
