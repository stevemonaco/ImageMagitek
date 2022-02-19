using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using TileShop.AvaloniaUI.Models;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.Shared.Input;
using KeyModifiers = TileShop.Shared.Input.KeyModifiers;

namespace TileShop.AvaloniaUI.Views;
public partial class IndexedPixelEditorView : UserControl
{
    private IndexedPixelEditorViewModel _viewModel = null!;
    private IndexedPixelEditorStateDriver _stateDriver = null!;
    private readonly Image _image;

    public IndexedPixelEditorView()
    {
        InitializeComponent();
        _image = this.FindControl<Image>("image");
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        _viewModel = (IndexedPixelEditorViewModel)DataContext!;
        _stateDriver = new(_viewModel);
        base.OnDataContextChanged(e);
    }

    private void OnPaletteEntryPressed(object sender, PointerPressedEventArgs e)
    {
        var entry = (PaletteEntry)sender!;
        var properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsLeftButtonPressed)
            _viewModel.SetPrimaryColor(entry.Index);
        else if (properties.IsRightButtonPressed)
            _viewModel.SetSecondaryColor(entry.Index);
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
        //if (e.Pointer.Type == PointerType.Mouse)
        //{
        //    var modifiers = CreateKeyModifiers(e.KeyModifiers);

        //    if (e.Delta.Y > 0)
        //    {
        //        _stateDriver.MouseWheel(MouseWheelDirection.Up, modifiers);
        //        //e.Handled = true;
        //    }
        //    else if (e.Delta.Y < 0)
        //    {
        //        _stateDriver.MouseWheel(MouseWheelDirection.Down, modifiers);
        //        //e.Handled = true;
        //    }
        //}
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
