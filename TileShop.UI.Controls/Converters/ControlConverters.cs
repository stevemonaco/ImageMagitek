using Avalonia.Data.Converters;

namespace TileShop.UI.Controls.Converters;

internal static class ControlConverters
{
    public static IValueConverter DoubleToHalf { get; } =
        new FuncValueConverter<double, double>(x => x / 2);
}