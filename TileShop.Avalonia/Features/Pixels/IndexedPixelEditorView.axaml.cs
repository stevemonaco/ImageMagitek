using System;
using System.Drawing;
using Avalonia.Controls;
using Avalonia.Input;
using TileShop.AvaloniaUI.Input;
using TileShop.AvaloniaUI.Models;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.Shared.Input;

namespace TileShop.AvaloniaUI.Views;
public partial class IndexedPixelEditorView : UserControl, IStateViewDriver<IndexedPixelEditorViewModel>
{
    public IndexedPixelEditorViewModel? ViewModel { get; private set; } = null!;

    public IndexedPixelEditorView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (DataContext is IndexedPixelEditorViewModel vm)
        {
            ViewModel = vm;
            ViewModel.OnImageModified = () => _image.InvalidateVisual();
        }
        base.OnDataContextChanged(e);
    }

    public void OnPaletteEntryPressed(object sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is PaletteEntry entry && ViewModel is not null)
        {
            var properties = e.GetCurrentPoint(this).Properties;

            if (properties.IsLeftButtonPressed)
                ViewModel?.SetPrimaryColor(entry.Index);
            else if (properties.IsRightButtonPressed)
                ViewModel?.SetSecondaryColor(entry.Index);

            e.Handled = true;
        }
    }

    public void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not null && ViewModel.LastMousePosition is Point point)
        {
            var state = InputAdapter.CreateKeyState(e.Key, e.KeyModifiers);
            ViewModel.KeyPress(state, point.X, point.Y);
            e.Handled = true;
        }
    }

    public void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not null && ViewModel.LastMousePosition is Point point)
        {
            var state = InputAdapter.CreateKeyState(e.Key, e.KeyModifiers);
            ViewModel.KeyUp(state, point.X, point.Y);
            e.Handled = true;
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

            Canvas.SetLeft(_penPreview, (int)point.Position.X);
            Canvas.SetTop(_penPreview, (int)point.Position.Y);
        }
    }

    public void OnPointerExited(object sender, PointerEventArgs e)
    {
        ViewModel?.MouseLeave();
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
