using System;
using System.Globalization;
using System.Windows.Data;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF.Converters
{
    public class ActiveDocumentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ToolViewModel)
                return value;

            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ToolViewModel)
                return value;

            return Binding.DoNothing;
        }
    }
}
