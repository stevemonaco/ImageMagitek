using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TileShop.Shared.Models;
using TileShop.UI.Controls;
using TileShop.UI.Renderer;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Views;

public partial class ImportImageView : UserControl
{
    private readonly ImportPreviewRenderer _renderer = new();
    private ImportImageViewModel? _viewModel;
    private bool _isFitPending = true;

    public ImportImageView()
    {
        InitializeComponent();

        PreviewCanvas.PaintSurface += OnPaintSurface;
        PreviewCanvas.PointerPressed += OnCanvasPointerPressed;
        PreviewCanvas.PointerReleased += OnCanvasPointerReleased;
        PreviewCanvas.PointerMoved += OnCanvasPointerMoved;
        PreviewCanvas.PointerExited += OnCanvasPointerExited;
        PreviewCanvas.PointerWheelChanged += OnCanvasPointerWheelChanged;
        PreviewCanvas.SizeChanged += OnCanvasSizeChanged;

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        _viewModel = DataContext as ImportImageViewModel;

        if (_viewModel is { } vm)
        {
            vm.OnPreviewReplaced = RebuildPreview;
            vm.OnInvalidated = PreviewCanvas.Invalidate;
            vm.OnZoomIn = () => ZoomBy(PreviewCanvas.ZoomPower, PreviewCanvas.Bounds.Center);
            vm.OnZoomOut = () => ZoomBy(1 / PreviewCanvas.ZoomPower, PreviewCanvas.Bounds.Center);
            vm.OnFitToViewport = FitToViewport;
            vm.OnResetZoom = ResetZoom;
            vm.OnCenterOn = CenterOn;
            RebuildPreview();
        }

        base.OnDataContextChanged(e);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _renderer.Dispose();
    }

    private void RebuildPreview()
    {
        if (_viewModel is { } vm)
            _renderer.SetPreview(vm.Arranger, vm.Preview);

        PreviewCanvas.Invalidate();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_viewModel is { } vm)
            _renderer.Render(vm, e.Surface.Canvas);
    }

    private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isFitPending && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            _isFitPending = false;
            FitToViewport();
        }
    }

    /// <summary>
    /// Fits the arranger with a little margin, snapping to a whole-number zoom when magnified so pixels stay crisp
    /// </summary>
    private void FitToViewport()
    {
        if (_viewModel is not { } vm)
            return;

        var size = vm.Arranger.ArrangerPixelSize;
        PreviewCanvas.FitToViewport(size.Width, size.Height);

        var zoom = PreviewCanvas.Zoom * 0.95;
        PreviewCanvas.Zoom = zoom >= 1 ? Math.Floor(zoom) : zoom;
        PreviewCanvas.CenterContent(size.Width, size.Height);
    }

    private void ResetZoom()
    {
        if (_viewModel is not { } vm)
            return;

        PreviewCanvas.ResetZoom();
        PreviewCanvas.CenterContent(vm.Arranger.ArrangerPixelSize.Width, vm.Arranger.ArrangerPixelSize.Height);
    }

    /// <summary>
    /// Scales the zoom while keeping the image point under <paramref name="screenPoint"/> fixed
    /// </summary>
    private void ZoomBy(double factor, Point screenPoint)
    {
        var local = PreviewCanvas.ScreenToLocalPoint(screenPoint);
        var zoom = Math.Clamp(PreviewCanvas.Zoom * factor, PreviewCanvas.MinZoom, PreviewCanvas.MaxZoom);

        PreviewCanvas.Zoom = zoom;
        PreviewCanvas.OffsetX = local.X * zoom - screenPoint.X;
        PreviewCanvas.OffsetY = local.Y * zoom - screenPoint.Y;
        PreviewCanvas.Invalidate();
    }

    private void CenterOn(System.Drawing.Point pixel)
    {
        var zoom = PreviewCanvas.Zoom;
        PreviewCanvas.MoveToPoint(new Point((pixel.X + 0.5) * zoom, (pixel.Y + 0.5) * zoom), PointAlignment.Center);
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(PreviewCanvas);

        if (point.Properties.IsLeftButtonPressed || point.Properties.IsMiddleButtonPressed)
        {
            PreviewCanvas.StartPan(point.Position);
            e.Pointer.Capture(PreviewCanvas);
            e.Handled = true;
        }
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        PreviewCanvas.EndPan();
        e.Pointer.Capture(null);
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel is not { } vm)
            return;

        var local = PreviewCanvas.ScreenToLocalPoint(e.GetPosition(PreviewCanvas));
        vm.UpdateHover((int)Math.Floor(local.X), (int)Math.Floor(local.Y));
    }

    private void OnCanvasPointerExited(object? sender, PointerEventArgs e)
    {
        _viewModel?.UpdateHover(-1, -1);
    }

    private void OnCanvasPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y == 0)
            return;

        var factor = e.Delta.Y > 0 ? PreviewCanvas.ZoomPower : 1 / PreviewCanvas.ZoomPower;
        ZoomBy(factor, e.GetPosition(PreviewCanvas));
        e.Handled = true;
    }

    private void OnEntryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel is { } vm && EntryList.SelectedItem is ImportColorEntryViewModel entry)
            vm.LocateEntryCommand.Execute(entry);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is not { } vm)
            return;

        var isControl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        e.Handled = e.Key switch
        {
            Key.OemPlus or Key.Add when !isControl => Invoke(vm.OnZoomIn),
            Key.OemMinus or Key.Subtract when !isControl => Invoke(vm.OnZoomOut),
            Key.W when isControl => Invoke(vm.OnFitToViewport),
            Key.R when isControl => Invoke(vm.OnResetZoom),
            Key.D1 or Key.NumPad1 => SetMode(vm, ImportPreviewMode.Current),
            Key.D2 or Key.NumPad2 => SetMode(vm, ImportPreviewMode.Imported),
            Key.D3 or Key.NumPad3 => SetMode(vm, ImportPreviewMode.OnionSkin),
            Key.D4 or Key.NumPad4 => SetMode(vm, ImportPreviewMode.Diff),
            _ => false
        };

        static bool Invoke(Action? action)
        {
            action?.Invoke();
            return true;
        }

        static bool SetMode(ImportImageViewModel vm, ImportPreviewMode mode)
        {
            vm.PreviewMode = mode;
            return true;
        }
    }
}
