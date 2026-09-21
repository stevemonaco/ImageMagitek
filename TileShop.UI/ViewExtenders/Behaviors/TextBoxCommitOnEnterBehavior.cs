using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace TileShop.UI.Behaviors;

/// <summary>
/// Lets Enter push a LostFocus-triggered Text binding to its source without leaving the TextBox
/// </summary>
public class TextBoxCommitOnEnterBehavior : Behavior<TextBox>
{
    protected override void OnAttachedToVisualTree()
    {
        AssociatedObject?.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree()
    {
        AssociatedObject?.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || AssociatedObject is null || AssociatedObject.AcceptsReturn)
            return;

        BindingOperations.GetBindingExpressionBase(AssociatedObject, TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }
}
