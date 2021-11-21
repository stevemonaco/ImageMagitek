using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace TileShop.WPF.Behaviors;

/// <summary>
/// Captures mouse wheel behavior for ScrollViewer when ctrl is held down
/// </summary>
internal class CaptureCtrlWheelMouseBehavior : Behavior<ScrollViewer>
{
    public static readonly DependencyProperty ProxyProperty = DependencyProperty.RegisterAttached(
        "Proxy",
        typeof(IMouseCaptureProxy),
        typeof(CaptureCtrlWheelMouseBehavior),
        new PropertyMetadata(null, OnProxyChanged));

    public static void SetProxy(DependencyObject source, IMouseCaptureProxy value)
    {
        source.SetValue(ProxyProperty, value);
    }

    public static IMouseCaptureProxy GetProxy(DependencyObject source)
    {
        return (IMouseCaptureProxy)source.GetValue(ProxyProperty);
    }

    private static void OnProxyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewMouseWheel += PreviewMouseWheel;
    }

    protected override void OnDetaching()
    {
        this.AssociatedObject.PreviewMouseWheel -= PreviewMouseWheel;
        base.OnDetaching();
    }

    private void PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var proxy = GetProxy(this);
        if (proxy is not null && e.Delta != 0 && (Keyboard.Modifiers == ModifierKeys.Control))
        {
            var pos = e.GetPosition(AssociatedObject);
            var direction = e.Delta switch
            {
                > 0 => MouseWheelDirection.Up,
                < 0 => MouseWheelDirection.Down,
                _ => MouseWheelDirection.None
            };

            var args = new MouseCaptureArgs
            {
                X = pos.X,
                Y = pos.Y,
                LeftButton = (e.LeftButton == MouseButtonState.Pressed),
                RightButton = (e.RightButton == MouseButtonState.Pressed),
                WheelDirection = direction
            };
            proxy.OnMouseWheel(this, args);

            e.Handled = true;
        }
    }
}
