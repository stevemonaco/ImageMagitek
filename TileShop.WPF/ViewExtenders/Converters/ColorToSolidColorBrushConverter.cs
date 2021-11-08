using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TileShop.WPF.Converters;

public class ColorToSolidColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return null;

        if (value is Color color)
            return new SolidColorBrush(color);

        throw new InvalidOperationException($"{nameof(ColorToSolidColorBrushConverter)}.{nameof(Convert)} cannot convert from given type {value.GetType()}");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
