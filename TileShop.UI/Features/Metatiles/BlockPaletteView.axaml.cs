using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TileShop.UI.Controls;
using TileShop.UI.Features.Graphics;
using TileShop.UI.Renderer;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Views;

public partial class BlockPaletteView : UserControl
{
    private readonly BlockPaletteRenderer _renderer = new();
    private readonly PaletteCanvasInput _input;
    private BlockPaletteViewModel? _viewModel;

    public BlockPaletteView()
    {
        InitializeComponent();

        BlockPaletteCanvas.PaintSurface += OnPaintSurface;
        _input = new PaletteCanvasInput(BlockPaletteCanvas, () => _viewModel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        // A floated or re-docked view may already have installed its own callback on this VM
        if (_viewModel is { } previous && ReferenceEquals(previous.OnInvalidated?.Target, BlockPaletteCanvas))
            previous.OnInvalidated = null;

        _viewModel = DataContext as BlockPaletteViewModel;

        if (_viewModel is { } vm)
        {
            vm.OnInvalidated = BlockPaletteCanvas.Invalidate;
            _input.FitWidth();
        }

        base.OnDataContextChanged(e);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _renderer.Dispose();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_viewModel is { } vm)
            _renderer.Render(vm, e.Surface.Canvas, BlockPaletteCanvas.Zoom);
    }
}
