using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Views;
public partial class PaletteEditorView : UserControl
{
    public PaletteEditorView()
    {
        InitializeComponent();

        // Tunnels so the modifiers are recorded before the swatch Button raises Click on release
        SwatchGrid.AddHandler(PointerReleasedEvent, OnSwatchPointerReleased, RoutingStrategies.Tunnel);
    }

    private void OnSwatchPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is PaletteEditorViewModel viewModel)
            viewModel.SetClickModifiers(e.KeyModifiers);
    }
}
