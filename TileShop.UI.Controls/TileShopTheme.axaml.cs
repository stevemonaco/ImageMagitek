using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace TileShop.UI.Controls;
public class TileShopTheme : Styles
{
    public TileShopTheme(IServiceProvider? sp = null)
    {
        AvaloniaXamlLoader.Load(sp, this);
    }
}