using System;
using System.Globalization;
using System.Windows.Data;
using TileShop.Shared.Models;

namespace TileShop.WPF.Converters;

public class SnapModeBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SnapMode mode)
        {
            if (mode == SnapMode.Element)
                return true;
            else if (mode == SnapMode.Pixel)
                return false;
        }

        return Binding.DoNothing;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool snapBool)
        {
            if (snapBool == true)
                return SnapMode.Element;
            else
                return SnapMode.Pixel;
        }

        return Binding.DoNothing;
    }
}
