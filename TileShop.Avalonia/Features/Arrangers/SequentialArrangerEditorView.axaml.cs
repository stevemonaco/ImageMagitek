using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.Shared.Input;

using KeyModifiers = TileShop.Shared.Input.KeyModifiers;

namespace TileShop.AvaloniaUI.Views;
public partial class SequentialArrangerEditorView : UserControl
{
    private SequentialArrangerEditorViewModel? _viewModel;

    public SequentialArrangerEditorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (DataContext is SequentialArrangerEditorViewModel vm)
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
            var state = CreateMouseState(point, e.KeyModifiers);
            _viewModel.MouseDown(point.Position.X, point.Position.Y, state);
            //e.Handled = true;
        }
    }

    private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && _viewModel is not null)
        {
            var point = e.GetCurrentPoint(_image);
            var state = CreateMouseState(point, e.KeyModifiers);
            _viewModel.MouseUp(point.Position.X, point.Position.Y, state);
            //e.Handled = true;
        }
    }

    private void OnPointerMoved(object sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && _viewModel is not null)
        {
            var point = e.GetCurrentPoint(_image);
            var state = CreateMouseState(point, e.KeyModifiers);
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
            var modifiers = CreateKeyModifiers(e.KeyModifiers);

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

    private MouseState CreateMouseState(PointerPoint point, Avalonia.Input.KeyModifiers keys)
    {
        var properties = point.Properties;
        var modifiers = CreateKeyModifiers(keys);

        return new MouseState(properties.IsLeftButtonPressed, properties.IsMiddleButtonPressed, properties.IsRightButtonPressed, modifiers);
    }

    private KeyModifiers CreateKeyModifiers(Avalonia.Input.KeyModifiers keys)
    {
        var modifiers = KeyModifiers.None;
        if (keys.HasFlag(Avalonia.Input.KeyModifiers.Alt))
            modifiers |= KeyModifiers.Alt;

        if (keys.HasFlag(Avalonia.Input.KeyModifiers.Control))
            modifiers |= KeyModifiers.Ctrl;

        if (keys.HasFlag(Avalonia.Input.KeyModifiers.Shift))
            modifiers = KeyModifiers.Shift;

        return modifiers;
    }
}
