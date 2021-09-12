using ImageMagitek.Colors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TileShop.WPF.Converters
{
    public class ColorRgba32ToMediaColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ColorRgba32 color)
                return new Color() { R = color.R, G = color.G, B = color.B, A = color.A };

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
                return new ColorRgba32(color.R, color.G, color.B, color.A);

            return DependencyProperty.UnsetValue;
        }
    }
}
