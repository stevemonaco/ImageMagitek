using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TileShop.UI.Controls;
using TileShop.UI.Renderer;

namespace TileShop.UI.Features.Graphics;

public partial class TilePaletteView : UserControl
{
    private readonly TilePaletteRenderer _renderer = new();
    private TilePaletteViewModel? _viewModel;
    private bool _isFitPending = true;

    public TilePaletteView()
    {
        InitializeComponent();

        TilePaletteCanvas.PaintSurface += OnPaintSurface;
        TilePaletteCanvas.PointerPressed += OnCanvasPointerPressed;
        TilePaletteCanvas.PointerReleased += OnCanvasPointerReleased;
        TilePaletteCanvas.PointerMoved += OnCanvasPointerMoved;
        TilePaletteCanvas.PointerExited += OnCanvasPointerExited;
        TilePaletteCanvas.PointerWheelChanged += OnCanvasPointerWheelChanged;
        TilePaletteCanvas.SizeChanged += OnCanvasSizeChanged;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        // A floated or re-docked view may already have installed its own callbacks on this VM
        if (_viewModel is { } previous && ReferenceEquals(previous.OnPaletteReplaced?.Target, this))
        {
            previous.OnInvalidated = null;
            previous.OnPaletteReplaced = null;
        }

        _viewModel = DataContext as TilePaletteViewModel;

        if (_viewModel is { } vm)
        {
            vm.OnInvalidated = TilePaletteCanvas.Invalidate;
            vm.OnPaletteReplaced = FitWidth;
            FitWidth();
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

    private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isFitPending && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            _isFitPending = false;
            FitWidth();
        }
    }

    /// <summary>
    /// Fits the palette's width at a whole-number zoom so tiles stay crisp, then shows its top rows
    /// </summary>
    private void FitWidth()
    {
        if (_viewModel is not { } vm || TilePaletteCanvas.Bounds.Width <= 0)
            return;

        var width = vm.Palette.Arranger.ArrangerPixelSize.Width;
        var zoom = Math.Floor(TilePaletteCanvas.Bounds.Width / width);
        TilePaletteCanvas.Zoom = Math.Clamp(zoom, TilePaletteCanvas.MinZoom, TilePaletteCanvas.MaxZoom);
        TilePaletteCanvas.AlignTopLeft();
    }

    private (int X, int Y) CellAt(PointerEventArgs e)
    {
        var vm = _viewModel!;
        var local = TilePaletteCanvas.ScreenToLocalPoint(e.GetPosition(TilePaletteCanvas));
        var size = vm.Palette.Arranger.ElementPixelSize;
        return ((int)Math.Floor(local.X / size.Width), (int)Math.Floor(local.Y / size.Height));
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is not { } vm)
            return;

        var point = e.GetCurrentPoint(TilePaletteCanvas);

        if (point.Properties.IsLeftButtonPressed)
        {
            var (x, y) = CellAt(e);
            vm.PointerDown(x, y);
            e.Pointer.Capture(TilePaletteCanvas);
            e.Handled = true;
        }
        else if (point.Properties.IsMiddleButtonPressed)
        {
            TilePaletteCanvas.StartPan(point.Position);
            e.Pointer.Capture(TilePaletteCanvas);
            e.Handled = true;
        }
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        TilePaletteCanvas.EndPan();
        _viewModel?.PointerUp();
        e.Pointer.Capture(null);
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel is not { } vm)
            return;

        var (x, y) = CellAt(e);
        vm.PointerMove(x, y);
    }

    private void OnCanvasPointerExited(object? sender, PointerEventArgs e) => _viewModel?.PointerExited();

    private void OnCanvasPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y == 0)
            return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Delta.Y > 0)
                TilePaletteCanvas.ZoomIn();
            else
                TilePaletteCanvas.ZoomOut();
        }
        else
        {
            TilePaletteCanvas.OffsetY = Math.Max(0, TilePaletteCanvas.OffsetY - e.Delta.Y * 48);
            TilePaletteCanvas.Invalidate();
        }

        e.Handled = true;
    }
}
