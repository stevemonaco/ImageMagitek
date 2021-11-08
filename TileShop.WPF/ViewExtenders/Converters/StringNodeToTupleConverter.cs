using System;
using System.Globalization;
using System.Windows.Data;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF.Converters;

public class StringNodeToTupleConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values?.Length != 2)
            return Binding.DoNothing;

        if (values[0] is string item1 && values[1] is ResourceNodeViewModel item2)
            return (item1, item2);
        else
            return Binding.DoNothing;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
