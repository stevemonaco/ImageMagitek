using System;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using TileShop.Shared.Input;
using TileShop.UI.Input;
using TileShop.UI.ViewModels;
using Point = System.Drawing.Point;

namespace TileShop.UI.Views
{
    public abstract class PixelEditorView<TViewModel> : UserControl, IStateViewDriver<TViewModel>
        where TViewModel : ArrangerEditorViewModel
    {
        public TViewModel ViewModel => (TViewModel)DataContext!;
        protected Image? _image;
        protected Rectangle? _penPreview;

        public void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (ViewModel.LastMousePosition is Point point)
            {
                var state = InputAdapter.CreateKeyState(e.Key, e.KeyModifiers);
                ViewModel.KeyPress(state, point.X, point.Y);
                e.Handled = true;
            }
        }

        public void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (ViewModel.LastMousePosition is Point point)
            {
                var state = InputAdapter.CreateKeyState(e.Key, e.KeyModifiers);
                ViewModel.KeyUp(state, point.X, point.Y);
                e.Handled = true;
            }
        }

        public void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.Pointer.Type == PointerType.Mouse)
            {
                var point = e.GetCurrentPoint(_image);
                var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
                ViewModel.MouseDown(point.Position.X, point.Position.Y, state);
            }
        }

        public void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (e.Pointer.Type == PointerType.Mouse)
            {
                var point = e.GetCurrentPoint(_image);
                var state = InputAdapter.CreateMouseState(point, e.KeyModifiers);
                ViewModel.MouseUp(point.Position.X, point.Position.Y, state);
            }
        }

        public void OnPointerMoved(object sender, PointerEventArgs e)
        {
            if (e.Pointer.Type == PointerType.Mouse)
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
            ViewModel.MouseLeave();
        }

        public void OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (e.Pointer.Type == PointerType.Mouse)
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

        protected override void OnDataContextChanged(EventArgs e)
        {
            if (DataContext is ArrangerEditorViewModel vm && _image is not null)
            {
                vm.OnImageModified = () => _image.InvalidateVisual();
            }
            base.OnDataContextChanged(e);
        }
    }
}
