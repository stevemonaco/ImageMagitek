using System;
using Avalonia.Controls;
using Avalonia.Input;
using TileShop.UI.Input;
using TileShop.Shared.Input;
using TileShop.Shared.Models;
using TileShop.UI.Controls;
using TileShop.UI.Renderer;
using TileShop.UI.ViewModels;
using KeyModifiers = Avalonia.Input.KeyModifiers;

namespace TileShop.UI.Views;
public partial class GraphicsEditorView : UserControl
{
    public GraphicsEditorViewModel ViewModel => (GraphicsEditorViewModel)DataContext!;

    private ArrangerRenderer? _renderer;

    public GraphicsEditorView()
    {
        InitializeComponent();

// #if DEBUG
//         EditorCanvas.ShowFrameTimings = true;
// #endif

        EditorCanvas.PaintSurface += OnPaintSurface;
        EditorCanvas.PointerPressed += CanvasOnPointerPressed;
        EditorCanvas.PointerReleased += CanvasOnPointerReleased;
        EditorCanvas.PointerMoved += CanvasOnPointerMoved;
        EditorCanvas.PointerExited += CanvasOnPointerExited;
        EditorCanvas.PointerWheelChanged += CanvasOnPointerWheelChanged;
        EditorCanvas.PointerCaptureLost += CanvasOnPointerCaptureLost;

        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        _renderer?.Render(ViewModel, e.Surface.Canvas);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (DataContext is GraphicsEditorViewModel vm)
        {
            _renderer = new ArrangerRenderer(vm.WorkingArranger);
            vm.OnImageModified = () => EditorCanvas.Invalidate();
            vm.OnCenterContent = () => EditorCanvas.CenterContent(vm.WorkingArranger.ArrangerPixelSize.Width, vm.WorkingArranger.ArrangerPixelSize.Height);
            vm.OnFitToViewport = () => EditorCanvas.FitToViewport(vm.WorkingArranger.ArrangerPixelSize.Width, vm.WorkingArranger.ArrangerPixelSize.Height);
            vm.OnResetZoom = () => EditorCanvas.ResetZoom();
            vm.OnAlignTopLeft = () => EditorCanvas.AlignTopLeft();
        }

        base.OnDataContextChanged(e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel.LastMousePosition is { } point)
        {
            var state = InputAdapter.CreateKeyState(e.Key, e.KeyModifiers);
            ViewModel.KeyPress(state, point.X, point.Y);
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (ViewModel.LastMousePosition is { } point)
        {
            var state = InputAdapter.CreateKeyState(e.Key, e.KeyModifiers);
            ViewModel.KeyUp(state, point.X, point.Y);
        }
    }

    private void CanvasOnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(EditorCanvas);
        var localPoint = EditorCanvas.ScreenToLocalPoint(point.Position);

        bool isHandled = false;

        if (ViewModel.ContainsPoint((int)localPoint.X, (int)localPoint.Y))
        {
            var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
            isHandled = ViewModel.MouseDown(localPoint.X, localPoint.Y, state);
        }

        if (!isHandled && point.Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed)
        {
            EditorCanvas.StartPan(point.Position);
            point.Pointer.Capture(EditorCanvas);
            isHandled = true;
        }

        e.Handled = isHandled;
    }

    public void CanvasOnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        var point = e.GetCurrentPoint(EditorCanvas);
        var localPoint = EditorCanvas.ScreenToLocalPoint(point.Position);

        bool isHandled = false;

        if (ViewModel.ContainsPoint((int)localPoint.X, (int)localPoint.Y))
        {
            var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
            isHandled = ViewModel.MouseUp(localPoint.X, localPoint.Y, state);
        }

        if (!isHandled && point.Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonReleased)
        {
            EditorCanvas.EndPan();
            point.Pointer.Capture(null);
            isHandled = false;
        }

        e.Handled = isHandled;
    }

    public void CanvasOnPointerMoved(object sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(EditorCanvas);
        var localPoint = EditorCanvas.ScreenToLocalPoint(point.Position);

        bool isHandled = false;

        if (ViewModel.ContainsPoint((int)localPoint.X, (int)localPoint.Y))
        {
            var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
            isHandled = ViewModel.MouseMove(localPoint.X, localPoint.Y, state);

            var handle = ViewModel.HitTestHandle(localPoint.X, localPoint.Y);
            EditorCanvas.Cursor = GetHandleCursor(handle);
        }
        else
        {
            EditorCanvas.Cursor = Cursor.Default;
        }

        e.Handled = isHandled;
    }

    private static Cursor GetHandleCursor(SelectionHandle handle) => handle switch
    {
        SelectionHandle.TopLeft or SelectionHandle.BottomRight => new Cursor(StandardCursorType.TopLeftCorner),
        SelectionHandle.TopRight or SelectionHandle.BottomLeft => new Cursor(StandardCursorType.TopRightCorner),
        SelectionHandle.Top or SelectionHandle.Bottom => new Cursor(StandardCursorType.SizeNorthSouth),
        SelectionHandle.Left or SelectionHandle.Right => new Cursor(StandardCursorType.SizeWestEast),
        _ => Cursor.Default,
    };

    public void CanvasOnPointerExited(object sender, PointerEventArgs e)
    {
        EditorCanvas.Cursor = Cursor.Default;
        e.Handled = ViewModel.MouseLeave();
    }

    public void CanvasOnPointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        var modifiers = InputAdapter.CreateKeyModifiers(e.KeyModifiers);

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Delta.Y > 0)
                EditorCanvas.ZoomIn();
            else if (e.Delta.Y < 0)
                EditorCanvas.ZoomOut();

            e.Handled = true;
        }
        else
        {
            if (e.Delta.Y > 0)
                e.Handled = ViewModel.MouseWheel(MouseWheelDirection.Up, modifiers);
            else if (e.Delta.Y < 0)
                e.Handled = ViewModel.MouseWheel(MouseWheelDirection.Down, modifiers);
        }
    }

    private void CanvasOnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EditorCanvas.EndPan();
    }
}
