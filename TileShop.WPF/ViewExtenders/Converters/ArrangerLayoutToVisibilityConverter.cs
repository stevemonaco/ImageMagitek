using ImageMagitek;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace TileShop.WPF.Converters
{
    public class ArrangerLayoutToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var parameterString = parameter as string;

            if (value is ArrangerLayout state && Enum.TryParse(parameterString, out ArrangerLayout stateVisibility))
            {
                var visibility = (state, stateVisibility) switch
                {
                    (ArrangerLayout.Tiled, ArrangerLayout.Tiled) => Visibility.Visible,
                    (ArrangerLayout.Single, ArrangerLayout.Single) => Visibility.Visible,
                    _ => Visibility.Collapsed
                };

                return visibility;
            }

            return Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
