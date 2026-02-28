using Avalonia.Controls;
using Avalonia.Interactivity;
using TileShop.UI.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Views;
public partial class TableColorView : UserControl
{
    public TableColorView()
    {
        InitializeComponent();
    }

    private void OnColorTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is Border { DataContext: ValidatedTableColorModel model } &&
            DataContext is TableColorViewModel vm)
        {
            vm.SetWorkingColor(model);
        }
    }
}
