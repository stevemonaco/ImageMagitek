using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TileShop.AvaloniaUI.Views;
public partial class Color32View : UserControl
{
    public Color32View()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
