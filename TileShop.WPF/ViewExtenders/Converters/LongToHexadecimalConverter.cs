using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace TileShop.WPF.Converters;

public class LongToHexadecimalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return null;

        if (value is long number)
            return $"{number:X}";

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return null;

        if (value is string hexString)
        {
            if (long.TryParse(hexString, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out long number))
            {
                return number;
            }
        }

        return DependencyProperty.UnsetValue;
    }
}
