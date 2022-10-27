using System;
using Avalonia.Controls;
using Avalonia.Input;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.AvaloniaUI.Input;
using TileShop.Shared.Input;

namespace TileShop.AvaloniaUI.Views;
public partial class ScatteredArrangerEditorView : UserControl
{
    private ScatteredArrangerEditorViewModel? _viewModel;

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
            var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
            _viewModel.MouseMove(point.Position.X, point.Position.Y, state);
            //e.Handled = true;
        }
    }

    private void OnPointerExited(object sender, PointerEventArgs e)
    {
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
