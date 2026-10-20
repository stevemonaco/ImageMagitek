using System;
using Avalonia.Controls;
using Avalonia.Input;
using TileShop.UI.Controls;
using TileShop.UI.Renderer;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Views;

public partial class MetatileMapEditorView : UserControl
{
    private readonly MetatileMapRenderer _renderer = new();
    private MetatileMapEditorViewModel? _viewModel;
    private Action? _imageModifiedCallback;
    private bool _isFitPending = true;

    public MetatileMapEditorView()
    {
        InitializeComponent();

        EditorCanvas.PaintSurface += OnPaintSurface;
        EditorCanvas.PointerPressed += OnCanvasPointerPressed;
        EditorCanvas.PointerReleased += OnCanvasPointerReleased;
        EditorCanvas.PointerCaptureLost += (_, _) => EditorCanvas.EndPan();
        EditorCanvas.PointerWheelChanged += OnCanvasPointerWheelChanged;
        EditorCanvas.SizeChanged += OnCanvasSizeChanged;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        // A floated or re-docked view may already have installed its own callbacks on this VM
        if (_viewModel is { } previous && ReferenceEquals(previous.OnImageModified, _imageModifiedCallback))
        {
            previous.OnImageModified = null;
            previous.OnFitToViewport = null;
            previous.OnResetZoom = null;
        }

        _viewModel = DataContext as MetatileMapEditorViewModel;

        if (_viewModel is { } vm)
        {
            _imageModifiedCallback = EditorCanvas.Invalidate;
            vm.OnImageModified = _imageModifiedCallback;
            vm.OnFitToViewport = FitToViewport;
            vm.OnResetZoom = EditorCanvas.ResetZoom;
        }

        base.OnDataContextChanged(e);
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_viewModel is { } vm)
            _renderer.Render(vm, e.Surface.Canvas);
    }

    private void FitToViewport()
    {
        if (_viewModel is { } vm)
            EditorCanvas.FitToViewport(vm.Map.PixelSize.Width, vm.Map.PixelSize.Height);
    }

    private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isFitPending && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            _isFitPending = false;
            FitToViewport();
        }
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(EditorCanvas);
        if (!point.Properties.IsMiddleButtonPressed)
            return;

        EditorCanvas.StartPan(point.Position);
        e.Pointer.Capture(EditorCanvas);
        e.Handled = true;
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Middle)
            return;

        EditorCanvas.EndPan();
        e.Pointer.Capture(null);
    }

    private void OnCanvasPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y == 0 || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        if (e.Delta.Y > 0)
            EditorCanvas.ZoomIn();
        else
            EditorCanvas.ZoomOut();

        e.Handled = true;
    }
}
