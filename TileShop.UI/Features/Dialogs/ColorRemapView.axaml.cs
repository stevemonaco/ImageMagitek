using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TileShop.UI.Models;

namespace TileShop.UI.Views;

public partial class ColorRemapView : UserControl
{
    private FlyoutBase? PickerFlyout => FlyoutBase.GetAttachedFlyout(RemapGrid);

    public ColorRemapView()
    {
        InitializeComponent();

        RemapGrid.ContextRequested += OnCellContextRequested;
    }

    private void OnCellClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control cell)
            PickerFlyout?.ShowAt(cell);
    }

    // Events inside the picker popup bubble through the cell it is anchored to, so only accept RemapGrid's own items
    private void OnCellContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        var container = (e.Source as Visual)?.FindAncestorOfType<ContentPresenter>(includeSelf: true);

        if (container is not null && RemapGrid.IndexFromContainer(container) >= 0 && container.DataContext is RemappableColorModel color)
        {
            color.Reset();
            e.Handled = true;
        }
    }

    private void OnPickerSwatchClick(object? sender, RoutedEventArgs e)
    {
        if (PickerFlyout?.Target?.DataContext is RemappableColorModel target && sender is Control { DataContext: RemappableColorModel source })
        {
            target.RemapTo(source);
            PickerFlyout.Hide();
        }
    }

    private void OnPickerResetClick(object? sender, RoutedEventArgs e)
    {
        if (PickerFlyout?.Target?.DataContext is RemappableColorModel target)
        {
            target.Reset();
            PickerFlyout.Hide();
        }
    }
}
