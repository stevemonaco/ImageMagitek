using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TileShop.UI.Controls;
using TileShop.UI.Renderer;

namespace TileShop.UI.Features.Graphics;

public partial class TilePaletteView : UserControl
{
    private readonly TilePaletteRenderer _renderer = new();
    private readonly PaletteCanvasInput _input;
    private TilePaletteViewModel? _viewModel;

    public TilePaletteView()
    {
        InitializeComponent();

        TilePaletteCanvas.PaintSurface += OnPaintSurface;
        _input = new PaletteCanvasInput(TilePaletteCanvas, () => _viewModel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        // A floated or re-docked view may already have installed its own callbacks on this VM
        if (_viewModel is { } previous && ReferenceEquals(previous.OnPaletteReplaced?.Target, _input))
        {
            previous.OnInvalidated = null;
            previous.OnPaletteReplaced = null;
        }

        _viewModel = DataContext as TilePaletteViewModel;

        if (_viewModel is { } vm)
        {
            vm.OnInvalidated = TilePaletteCanvas.Invalidate;
            vm.OnPaletteReplaced = _input.FitWidth;
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
            _renderer.Render(vm, e.Surface.Canvas, TilePaletteCanvas.Zoom);
    }
}
