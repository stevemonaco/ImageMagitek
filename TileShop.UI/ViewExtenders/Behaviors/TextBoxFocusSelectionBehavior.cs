using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace TileShop.UI.Behaviors;
public class TextBoxFocusSelectionBehavior : Behavior<TextBox>
{
    /// <inheritdoc />
    protected override void OnAttachedToVisualTree()
    {
        AssociatedObject?.AddHandler(InputElement.GotFocusEvent, AssociatedObject_GotFocus, RoutingStrategies.Bubble);
        AssociatedObject?.AddHandler(InputElement.PointerPressedEvent, AssociatedObject_PreviewPointerPressed, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree()
    {
        AssociatedObject?.RemoveHandler(InputElement.GotFocusEvent, AssociatedObject_GotFocus);
        AssociatedObject?.RemoveHandler(InputElement.PointerPressedEvent, AssociatedObject_PreviewPointerPressed);
    }

    private void AssociatedObject_GotFocus(object? sender, GotFocusEventArgs e)
    {
        AssociatedObject?.SelectAll();
    }

    private void AssociatedObject_PreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (AssociatedObject?.IsKeyboardFocusWithin is false)
        {
            AssociatedObject.Focus();
            AssociatedObject.SelectAll();
            e.Handled = true;
        }
    }
}
