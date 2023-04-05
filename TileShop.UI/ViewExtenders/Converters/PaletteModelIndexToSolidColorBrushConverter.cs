using System;
using System.Globalization;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Data.Converters;
using Avalonia;
using TileShop.UI.Models;

namespace TileShop.UI.Converters;

public class PaletteModelIndexToSolidColorBrushConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Count != 2)
            return AvaloniaProperty.UnsetValue;

        if (values[0] is PaletteModel pal && values[1] is byte index)
        {
            if (pal.Colors.Count > index)
            {
                var entry = pal.Colors[index];
                return new SolidColorBrush(entry.Color);
            }
        }

        return AvaloniaProperty.UnsetValue;
    }
}
