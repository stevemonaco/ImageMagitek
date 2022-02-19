using System;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.Shared.Input;

using KeyModifiers = TileShop.Shared.Input.KeyModifiers;

namespace TileShop.AvaloniaUI.Views;
public partial class ScatteredArrangerEditorView : UserControl
{
    private ScatteredArrangerStateDriver _stateDriver;
    private readonly Image _image;
    private readonly ZoomBorder _zoomBorder;

    public ScatteredArrangerEditorView()
    {
        InitializeComponent();
        _image = this.FindControl<Image>("image");
        _zoomBorder = this.FindControl<ZoomBorder>("zoomBorder");
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        var vm = (ScatteredArrangerEditorViewModel)DataContext!;
        _stateDriver = new ScatteredArrangerStateDriver(vm);
        base.OnDataContextChanged(e);
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse)
        {
            var point = e.GetCurrentPoint(_image);
            var state = CreateMouseState(point, e.KeyModifiers);
            _stateDriver.MouseDown(point.Position.X, point.Position.Y, state);
            //e.Handled = true;
        }
    }

    private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse)
        {
            var point = e.GetCurrentPoint(_image);
            var state = CreateMouseState(point, e.KeyModifiers);
            _stateDriver.MouseUp(point.Position.X, point.Position.Y, state);
            //e.Handled = true;
        }
    }

    private void OnPointerMoved(object sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(_image);
        _stateDriver.MouseMove(pos.X, pos.Y);
        //e.Handled = true;
    }

    private void OnPointerLeave(object sender, PointerEventArgs e)
    {
        _stateDriver.MouseLeave();
        //e.Handled = true;
    }

    private void OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse)
        {
            var modifiers = CreateKeyModifiers(e.KeyModifiers);

            if (e.Delta.Y > 0)
            {
                _stateDriver.MouseWheel(MouseWheelDirection.Up, modifiers);
                //e.Handled = true;
            }
            else if (e.Delta.Y < 0)
            {
                _stateDriver.MouseWheel(MouseWheelDirection.Down, modifiers);
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
