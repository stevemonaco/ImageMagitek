using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TileShop.AvaloniaUI.Views;
public partial class TableColorView : UserControl
{
    public TableColorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
