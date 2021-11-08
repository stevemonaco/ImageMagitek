using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TileShop.Shared.Models;

namespace TileShop.WPF.Converters;

public enum OverlayStateVisibility { Selection, Paste }
public class OverlayStateVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var parameterString = parameter as string;

        if (value is OverlayState state && Enum.TryParse(parameterString, out OverlayStateVisibility stateVisibility))
        {
            var visibility = (state, stateVisibility) switch
            {
                (OverlayState.Selecting, OverlayStateVisibility.Selection) => Visibility.Visible,
                (OverlayState.Selected, OverlayStateVisibility.Selection) => Visibility.Visible,
                (OverlayState.Pasting, OverlayStateVisibility.Paste) => Visibility.Visible,
                (OverlayState.Pasted, OverlayStateVisibility.Paste) => Visibility.Visible,
                _ => Visibility.Hidden
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
