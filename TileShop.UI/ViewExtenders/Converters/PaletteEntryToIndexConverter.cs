﻿using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using TileShop.UI.Models;

namespace TileShop.UI.Converters;

public class PaletteEntryToIndexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PaletteEntry entry)
            return entry.Index;

        return AvaloniaProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
