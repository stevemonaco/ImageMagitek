using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.DragAndDrop;
using TileShop.Shared.Tools;
using TileShop.UI.DragDrop;
using TileShop.UI.Input;
using TileShop.Shared.Input;
using TileShop.Shared.Models;
using TileShop.UI.Controls;
using TileShop.UI.Renderer;
using TileShop.UI.ViewExtenders.DragDrop;
using TileShop.UI.ViewModels;
using KeyModifiers = Avalonia.Input.KeyModifiers;

namespace TileShop.UI.Views;

using DragDrop = Avalonia.Input.DragDrop;

public partial class GraphicsEditorView : UserControl
{
    public GraphicsEditorViewModel ViewModel => (GraphicsEditorViewModel)DataContext!;

    private ArrangerRenderer? _renderer;

    private readonly ArrangerDragHandler _dragHandler = new();
    private Point _dragStartPoint;
    private PointerPressedEventArgs? _dragTriggerEvent;
    private bool _isDragPending;
    private const double _dragThreshold = 3;

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
        EditorCanvas.ContextRequested += CanvasOnContextRequested;

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

    private bool IsPointOnDraggable(double localX, double localY)
    {
        var vm = ViewModel;
        int xc = Math.Clamp((int)localX, 0, vm.WorkingArranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)localY, 0, vm.WorkingArranger.ArrangerPixelSize.Height - 1);

        if (vm.Selection.HasSelection && vm.Selection.SelectionRect.ContainsPointSnapped(xc, yc))
            return true;

        if (vm.Paste is not null && vm.Paste.Rect.ContainsPointSnapped(xc, yc))
            return true;

        return false;
    }

    private void CanvasOnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(EditorCanvas);
        var localPoint = EditorCanvas.ScreenToLocalPoint(point.Position);

        bool isHandled = false;

        if (ViewModel.ContainsPoint(localPoint.X, localPoint.Y))
        {
            var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);

            // Check if clicking on a draggable region before MouseDown processes it
            bool onDraggable = point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed
                && IsPointOnDraggable(localPoint.X, localPoint.Y);

            isHandled = ViewModel.MouseDown(localPoint.X, localPoint.Y, state);

            if (onDraggable)
            {
                _isDragPending = true;
                _dragStartPoint = e.GetPosition(null);
                _dragTriggerEvent = e;
            }
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
        _isDragPending = false;
        _dragTriggerEvent = null;

        var point = e.GetCurrentPoint(EditorCanvas);
        var localPoint = EditorCanvas.ScreenToLocalPoint(point.Position);

        bool isHandled = false;

        if (ViewModel.ContainsPoint(localPoint.X, localPoint.Y))
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

    public async void CanvasOnPointerMoved(object sender, PointerEventArgs e)
    {
        if (_isDragPending && _dragTriggerEvent is not null)
        {
            var currentPos = e.GetPosition(null);
            var diff = _dragStartPoint - currentPos;

            if (Math.Abs(diff.X) > _dragThreshold || Math.Abs(diff.Y) > _dragThreshold)
            {
                _isDragPending = false;
                var triggerEvent = _dragTriggerEvent;
                _dragTriggerEvent = null;

                _dragHandler.BeforeDragDrop(EditorCanvas, triggerEvent, ViewModel);

                var payload = _dragHandler.Payload;
                if (payload is not null)
                {
                    ViewModel.InvalidateEditor(InvalidationLevel.Overlay);

                    var dataTransfer = IDataTransfer.CreateInProcessTransfer(payload);
                    var effect = DragDropEffects.Move;
                    await DragDrop.DoDragDropAsync(triggerEvent, dataTransfer, effect);

                    _dragHandler.AfterDragDrop(EditorCanvas, triggerEvent, ViewModel);
                }

                return;
            }
        }

        var point = e.GetCurrentPoint(EditorCanvas);
        var localPoint = EditorCanvas.ScreenToLocalPoint(point.Position);

        bool isHandled = false;

        if (ViewModel.ContainsPoint(localPoint.X, localPoint.Y))
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

    private void CanvasOnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (ViewModel.EditMode == GraphicsEditMode.Draw && e.TryGetPosition(EditorCanvas, out var position))
        {
            var localPoint = EditorCanvas.ScreenToLocalPoint(position);
            if (ViewModel.ContainsPoint(localPoint.X, localPoint.Y))
            {
                e.Handled = true;
            }
        }
    }

    private void CanvasOnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isDragPending = false;
        _dragTriggerEvent = null;
        EditorCanvas.EndPan();
    }
}
