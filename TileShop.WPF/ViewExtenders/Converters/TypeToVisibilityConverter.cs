using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TileShop.WPF.Converters
{
    public class TypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Type parameterType = parameter as Type;
            if (parameterType is null || value is null)
                return Visibility.Collapsed;

            if (parameterType.IsAssignableFrom(value.GetType()))
                return Visibility.Visible;
            else
                return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
