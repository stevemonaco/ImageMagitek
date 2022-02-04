using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TileShop.AvaloniaUI.Views;
public partial class DockableToolView : UserControl
{
    public DockableToolView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
