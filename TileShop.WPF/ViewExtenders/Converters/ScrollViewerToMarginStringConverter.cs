using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace TileShop.WPF.Converters;

public class ScrollViewerToMarginStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return null;

        if (value is ScrollViewer scrollViewer)
        {
            scrollViewer.ApplyTemplate();
            var verticalScroll = scrollViewer.Template.FindName("PART_VerticalScrollBar", scrollViewer) as ScrollBar;
            var horizontalScroll = scrollViewer.Template.FindName("PART_HorizontalScrollBar", scrollViewer) as ScrollBar;

            return $"0,0,{verticalScroll.MinWidth},{horizontalScroll.MinHeight}";
        }

        throw new InvalidOperationException($"{nameof(ScrollViewerToMarginStringConverter)}.{nameof(Convert)} cannot convert from given type {value.GetType()}");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
