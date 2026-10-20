using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TileShop.Shared.Tools;
using TileShop.UI.Controls;
using TileShop.UI.Input;
using TileShop.UI.Renderer;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Views;

public partial class MetatileMapEditorView : UserControl
{
    private readonly MetatileMapRenderer _renderer = new();
    private MetatileMapEditorViewModel? _viewModel;
    private Action? _imageModifiedCallback;

    // The first size change can come before the canvas reaches its final size, so the map refits until the user zooms or pans
    private bool _isAutoFit = true;

    public MetatileMapEditorView()
    {
        InitializeComponent();

        EditorCanvas.PaintSurface += OnPaintSurface;
        EditorCanvas.PointerPressed += OnCanvasPointerPressed;
        EditorCanvas.PointerReleased += OnCanvasPointerReleased;
        EditorCanvas.PointerMoved += OnCanvasPointerMoved;
        EditorCanvas.PointerExited += OnCanvasPointerExited;
        EditorCanvas.PointerCaptureLost += OnCanvasPointerCaptureLost;
        EditorCanvas.PointerWheelChanged += OnCanvasPointerWheelChanged;
        EditorCanvas.SizeChanged += OnCanvasSizeChanged;

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
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
            vm.OnResetZoom = ResetZoom;
        }

        base.OnDataContextChanged(e);
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_viewModel is { } vm)
            _renderer.Render(vm, e.Surface.Canvas, EditorCanvas.Zoom);
    }

    private void FitToViewport()
    {
        if (_viewModel is { } vm)
            EditorCanvas.FitToViewport(vm.Map.PixelSize.Width, vm.Map.PixelSize.Height);
    }

    private void ResetZoom()
    {
        _isAutoFit = false;
        EditorCanvas.ResetZoom();
    }

    private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isAutoFit && e.NewSize.Width > 0 && e.NewSize.Height > 0)
            FitToViewport();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is not { } vm)
            return;

        e.Handled = vm.KeyPress(InputAdapter.CreateKeyState(e.Key, e.KeyModifiers));
        UpdateCursorAtLastPosition(e.KeyModifiers);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (_viewModel is not { } vm)
            return;

        vm.KeyUp(InputAdapter.CreateKeyState(e.Key, e.KeyModifiers));
        UpdateCursorAtLastPosition(e.KeyModifiers);
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is not { } vm)
            return;

        var point = e.GetCurrentPoint(EditorCanvas);
        if (point.Properties.IsMiddleButtonPressed)
        {
            _isAutoFit = false;
            EditorCanvas.StartPan(point.Position);
            e.Pointer.Capture(EditorCanvas);
            e.Handled = true;
            return;
        }

        var local = EditorCanvas.ScreenToLocalPoint(point.Position);
        if (vm.ContainsPoint(local.X, local.Y) && vm.MouseDown(local.X, local.Y, InputAdapter.CreateMouseState(point, e.KeyModifiers)))
        {
            e.Pointer.Capture(EditorCanvas);
            e.Handled = true;
        }
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Middle)
        {
            EditorCanvas.EndPan();
            e.Pointer.Capture(null);
            return;
        }

        if (_viewModel is not { } vm)
            return;

        var point = e.GetCurrentPoint(EditorCanvas);
        var local = EditorCanvas.ScreenToLocalPoint(point.Position);
        e.Handled = vm.MouseUp(local.X, local.Y, InputAdapter.CreateMouseState(point, e.KeyModifiers));
        e.Pointer.Capture(null);
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel is not { } vm)
            return;

        var point = e.GetCurrentPoint(EditorCanvas);
        var local = EditorCanvas.ScreenToLocalPoint(point.Position);
        e.Handled = vm.MouseMove(local.X, local.Y, InputAdapter.CreateMouseState(point, e.KeyModifiers));
        UpdateCursor(local, e.KeyModifiers);
    }

    private void OnCanvasPointerExited(object? sender, PointerEventArgs e)
    {
        EditorCanvas.Cursor = Cursor.Default;
        _viewModel?.MouseLeave();
    }

    private void OnCanvasPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EditorCanvas.EndPan();
        _viewModel?.PointerCaptureLost();
    }

    private void UpdateCursorAtLastPosition(KeyModifiers modifiers)
    {
        if (_viewModel?.LastMousePosition is { } position)
            UpdateCursor(new Point(position.X, position.Y), modifiers);
    }

    private void UpdateCursor(Point local, KeyModifiers modifiers)
    {
        if (_viewModel is not { } vm || !vm.ContainsPoint(local.X, local.Y))
        {
            EditorCanvas.Cursor = Cursor.Default;
            return;
        }

        var toolCursor = vm.ResolveCursor(InputAdapter.CreateKeyModifiers(modifiers));
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        EditorCanvas.Cursor = toolCursor == ToolCursor.Default ? Cursor.Default : ToolCursors.Get(toolCursor, scaling);
    }

    private void OnCanvasPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y == 0 || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        _isAutoFit = false;
        if (e.Delta.Y > 0)
            EditorCanvas.ZoomIn();
        else
            EditorCanvas.ZoomOut();

        e.Handled = true;
    }
}
