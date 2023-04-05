using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace TileShop.UI.Converters;

public class EnumToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string parameterString && value is Enum valueEnum)
        {
            if (Enum.IsDefined(valueEnum.GetType(), valueEnum) == false)
                return AvaloniaProperty.UnsetValue;

            object parameterValue = Enum.Parse(valueEnum.GetType(), parameterString);

            return parameterValue.Equals(value);
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string parameterString)
            return Enum.Parse(targetType, parameterString);

        return AvaloniaProperty.UnsetValue;
    }
}
