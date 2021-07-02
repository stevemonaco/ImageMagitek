using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using ImageMagitek;

namespace TileShop.WPF.Converters
{
    public class EndianToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Endian endian)
            {
                if (endian == Endian.Big)
                    return true;
                else if (endian == Endian.Little)
                    return false;
            }

            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool endianBool)
            {
                if (endianBool == true)
                    return Endian.Big;
                else
                    return Endian.Little;
            }

            return Binding.DoNothing;
        }
    }
}
