using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF.Converters
{
    public class EditModeBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is EditMode mode)
            {
                if (mode == EditMode.ArrangeGraphics)
                    return false;
                else if (mode == EditMode.ModifyGraphics)
                    return true;
            }

            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is bool modeBool)
            {
                if (modeBool == false)
                    return EditMode.ArrangeGraphics;
                else
                    return EditMode.ModifyGraphics;
            }

            return Binding.DoNothing;
        }
    }
}
