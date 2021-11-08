using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TileShop.WPF.Models;

namespace TileShop.WPF.Converters;

public class PaletteModelIndexToSolidColorBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is null || values.Length != 2)
            return DependencyProperty.UnsetValue;

        if (values[0] is PaletteModel pal && values[1] is byte index)
        {
            if (pal.Colors.Count > index)
            {
                var entry = pal.Colors[index];
                return new SolidColorBrush(entry.Color);
            }
        }

        return DependencyProperty.UnsetValue;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
