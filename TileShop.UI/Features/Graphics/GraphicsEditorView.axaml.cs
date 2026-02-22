using System;
using Avalonia.Controls;
using Avalonia.Input;
using TileShop.UI.Features.Graphics;
using TileShop.UI.Input;
using TileShop.Shared.Input;
using System.Drawing;
using Avalonia.Interactivity;
using TileShop.UI.Controls;
using TileShop.UI.Renderer;

namespace TileShop.UI.Views;
public partial class GraphicsEditorView : UserControl, IStateViewDriver<GraphicsEditorViewModel>
{
    public GraphicsEditorViewModel ViewModel => (GraphicsEditorViewModel)DataContext!;

    private ArrangerRenderer? _renderer;

    public GraphicsEditorView()
    {
        InitializeComponent();
        
#if DEBUG
        EditorCanvas.ShowFrameTimings = true;
#endif

        EditorCanvas.PaintSurface += OnPaintSurface;
        EditorCanvas.PointerPressed += CanvasOnPointerPressed;
        EditorCanvas.PointerReleased += CanvasOnPointerReleased;
        EditorCanvas.PointerMoved += CanvasOnPointerMoved;
        EditorCanvas.PointerExited += CanvasOnPointerExited;
        EditorCanvas.PointerWheelChanged += CanvasOnPointerWheelChanged;
        //EditorCanvas.KeyDown += CanvasOnKeyDown;
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
            //vm.OnImageModified = () => EditorImage.InvalidateVisual();
            //EditorImage.Source = vm.BitmapAdapter.Bitmap;
        }

        base.OnDataContextChanged(e);
    }

    public void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (ViewModel.LastMousePosition is { } point)
        {
            var state = InputAdapter.CreateKeyState(e.Key, e.KeyModifiers);
            ViewModel.KeyUp(state, point.X, point.Y);
        }
    }

    public void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        
    }

    public void OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        
    }

    public void OnPointerMoved(object sender, PointerEventArgs e)
    {
        
    }

    public void OnPointerExited(object sender, PointerEventArgs e)
    {
        
    }

    public void OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        
    }

    public void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel.LastMousePosition is { } point)
        {
            var state = InputAdapter.CreateKeyState(e.Key, e.KeyModifiers);
            ViewModel.KeyPress(state, point.X, point.Y);
        }
    }

    public void CanvasOnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var localPoint = EditorCanvas.ScreenToLocalPoint(point.Position);
        
        var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
        ViewModel.MouseDown(point.Position.X, point.Position.Y, state);
    }

    public void CanvasOnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
        ViewModel.MouseUp(point.Position.X, point.Position.Y, state);
    }

    public void CanvasOnPointerMoved(object sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
        ViewModel.MouseMove(point.Position.X, point.Position.Y, state);
    }

    public void CanvasOnPointerExited(object sender, PointerEventArgs e)
    {
        ViewModel.MouseLeave();
    }

    public void CanvasOnPointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        var modifiers = InputAdapter.CreateKeyModifiers(e.KeyModifiers);

        if (e.Delta.Y > 0)
        {
            ViewModel.MouseWheel(MouseWheelDirection.Up, modifiers);
        }
        else if (e.Delta.Y < 0)
        {
            ViewModel.MouseWheel(MouseWheelDirection.Down, modifiers);
        }
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.Render();
        // EditorImage.InvalidateImage();
    }
}
