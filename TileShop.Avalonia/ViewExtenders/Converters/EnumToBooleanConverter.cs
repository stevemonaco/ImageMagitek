using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace TileShop.AvaloniaUI.Converters;

public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string parameterString = parameter as string;
        if (parameterString is null)
            return AvaloniaProperty.UnsetValue;

        if (Enum.IsDefined(value.GetType(), value) == false)
            return AvaloniaProperty.UnsetValue;

        object parameterValue = Enum.Parse(value.GetType(), parameterString);

        return parameterValue.Equals(value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string parameterString = parameter as string;
        if (parameterString is null)
            return AvaloniaProperty.UnsetValue;

        return Enum.Parse(targetType, parameterString);
    }
}
