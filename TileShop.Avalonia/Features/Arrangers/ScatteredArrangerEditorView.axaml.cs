using System;
using Avalonia.Controls;
using Avalonia.Input;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.AvaloniaUI.Input;
using TileShop.Shared.Input;
using Avalonia.Interactivity;

namespace TileShop.AvaloniaUI.Views;
public partial class ScatteredArrangerEditorView : UserControl
{
    private ScatteredArrangerEditorViewModel? _viewModel;
    private double? _lastX;
    private double? _lastY;

    public ScatteredArrangerEditorView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (DataContext is ScatteredArrangerEditorViewModel vm)
        {
            _viewModel = vm;
            _viewModel.OnImageModified = () => _image.InvalidateVisual();
        }
        
        base.OnDataContextChanged(e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is not null && _lastX is not null && _lastY is not null)
        {
            var state = InputAdapter.CreateKeyState(e.Key, e.KeyModifiers);
            _viewModel.KeyPress(state, _lastX.Value, _lastY.Value);
            //_viewModel.KeyPress();
        }
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && _viewModel is not null)
        {
            var point = e.GetCurrentPoint(_image);
            var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
            _viewModel.MouseDown(point.Position.X, point.Position.Y, state);
            //e.Handled = true;
        }
    }

    private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && _viewModel is not null)
        {
            var point = e.GetCurrentPoint(_image);
            var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
            _viewModel.MouseUp(point.Position.X, point.Position.Y, state);
            //e.Handled = true;
        }
    }

    private void OnPointerMoved(object sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && _viewModel is not null)
        {
            var point = e.GetCurrentPoint(_image);
            _lastX = point.Position.X;
            _lastY = point.Position.Y;

            var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
            _viewModel.MouseMove(_lastX.Value, _lastY.Value, state);
            //e.Handled = true;
        }
    }

    private void OnPointerExited(object sender, PointerEventArgs e)
    {
        _lastX = null;
        _lastY = null;
        _viewModel?.MouseLeave();
        //e.Handled = true;
    }

    private void OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && _viewModel is not null)
        {
            var modifiers = InputAdapter.CreateKeyModifiers(e.KeyModifiers);

            if (e.Delta.Y > 0)
            {
                _viewModel.MouseWheel(MouseWheelDirection.Up, modifiers);
                //e.Handled = true;
            }
            else if (e.Delta.Y < 0)
            {
                _viewModel.MouseWheel(MouseWheelDirection.Down, modifiers);
                //e.Handled = true;
            }
        }
    }
}
