using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace TileShop.UI.Converters;

public class EnumToBoolConverter : IValueConverter
{
    /// <summary>
    /// Matches the enum member state against the parameter
    /// </summary>
    /// <param name="value">The enum member value</param>
    /// <param name="targetType">A boolean</param>
    /// <param name="parameter">The enum member, as string or the typed enum member</param>
    /// <param name="culture"></param>
    /// <returns>True if matched, false if not. UnsetValue or DoNothing if not a valid comparison</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Enum enumValue)
            return AvaloniaProperty.UnsetValue;

        if (parameter is string parameterString)
        {
            if (Enum.IsDefined(enumValue.GetType(), enumValue) == false)
                return BindingOperations.DoNothing;

            object parameterValue = Enum.Parse(enumValue.GetType(), parameterString);

            return parameterValue.Equals(value);
        }

        if (parameter is not null)
        {
            if (Enum.IsDefined(value.GetType(), enumValue))
            {
                return parameter.Equals(enumValue);
            }
        }

        return AvaloniaProperty.UnsetValue;
    }

    /// <summary>
    /// Returns the enum member parameter if the boolean state is true
    /// </summary>
    /// <param name="value">A true/false boolean value</param>
    /// <param name="targetType">The enum type</param>
    /// <param name="parameter">The enum member, as string or the typed enum member</param>
    /// <param name="culture"></param>
    /// <returns>If true, the parameter is returned as an enum member if valid. Otherwise, UnsetValue or DoNothing</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool shouldReturnEnum)
            return AvaloniaProperty.UnsetValue;

        if (!shouldReturnEnum)
            return BindingOperations.DoNothing;

        if (parameter is string enumTypeString && Enum.TryParse(targetType, enumTypeString, out var enumKind))
            return enumKind;

        if (parameter is not null && Enum.IsDefined(targetType, parameter))
            return parameter;

        return AvaloniaProperty.UnsetValue;
    }
}