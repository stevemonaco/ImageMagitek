using System;
using Avalonia.Controls;
using Avalonia.Input;
using TileShop.UI.ViewModels;
using TileShop.UI.Input;
using TileShop.Shared.Input;
using System.Drawing;

namespace TileShop.UI.Views;
public partial class ScatteredArrangerEditorView : UserControl, IStateViewDriver<ScatteredArrangerEditorViewModel>
{
    public ScatteredArrangerEditorViewModel? ViewModel { get; private set; }

    public ScatteredArrangerEditorView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (DataContext is ScatteredArrangerEditorViewModel vm)
        {
            ViewModel = vm;
            ViewModel.OnImageModified = () => _image.InvalidateVisual();
        }
        
        base.OnDataContextChanged(e);
    }

    public void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not null && ViewModel.LastMousePosition is Point point)
        {
            var state = InputAdapter.CreateKeyState(e.Key, e.KeyModifiers);
            ViewModel.KeyUp(state, point.X, point.Y);
        }
    }

    public void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not null && ViewModel.LastMousePosition is Point point)
        {
            var state = InputAdapter.CreateKeyState(e.Key, e.KeyModifiers);
            ViewModel.KeyPress(state, point.X, point.Y);
        }
    }

    public void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && ViewModel is not null)
        {
            var point = e.GetCurrentPoint(_image);
            var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
            ViewModel.MouseDown(point.Position.X, point.Position.Y, state);
        }
    }

    public void OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && ViewModel is not null)
        {
            var point = e.GetCurrentPoint(_image);
            var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
            ViewModel.MouseUp(point.Position.X, point.Position.Y, state);
        }
    }

    public void OnPointerMoved(object sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && ViewModel is not null)
        {
            var point = e.GetCurrentPoint(_image);

            var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
            ViewModel.MouseMove(point.Position.X, point.Position.Y, state);
        }
    }

    public void OnPointerExited(object sender, PointerEventArgs e)
    {
        if (ViewModel is null)
            return;

        var pos = e.GetCurrentPoint(_image).Position;

        if (pos.X < 0 || pos.X >= ViewModel.BitmapAdapter.Width || pos.Y < 0 || pos.Y >= ViewModel.BitmapAdapter.Height)
        {
            ViewModel?.MouseLeave();
        }
    }

    public void OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && ViewModel is not null)
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
    }
}
