using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace TileShop.UI.Converters;

public class SwatchSelectionBorderConverter : IMultiValueConverter
{
    private static readonly IBrush _primaryBrush = new SolidColorBrush(Colors.White);
    private static readonly IBrush _secondaryBrush = new SolidColorBrush(Color.Parse("#AAAAAA"));
    private static readonly IBrush _defaultBrush = Brushes.Transparent;

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Count != 3)
            return _defaultBrush;

        if (values[0] is byte entryIndex && values[1] is byte primaryIndex && values[2] is byte secondaryIndex)
        {
            if (entryIndex == primaryIndex)
                return _primaryBrush;
            if (entryIndex == secondaryIndex)
                return _secondaryBrush;
        }

        return _defaultBrush;
    }
}
