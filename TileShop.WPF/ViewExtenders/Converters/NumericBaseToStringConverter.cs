using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF.Converters
{
    public class NumericBaseToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NumericBase numericBase)
            {
                return numericBase switch
                {
                    NumericBase.Decimal => "Dec",
                    NumericBase.Hexadecimal => "Hex",
                    _ => throw new InvalidOperationException($"{nameof(NumericBaseToStringConverter)}.{nameof(Convert)} cannot convert from given type {value.GetType()} with value {value}"),
                };
            }

            throw new InvalidOperationException($"{nameof(NumericBaseToStringConverter)}.{nameof(Convert)} cannot convert from given type {value.GetType()}");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
