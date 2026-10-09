using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TileShop.UI.Views;
public partial class MenuView : UserControl
{
    public MenuView()
    {
        InitializeComponent();
    }

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        (TopLevel.GetTopLevel(this) as Window)?.Close();
    }
}
