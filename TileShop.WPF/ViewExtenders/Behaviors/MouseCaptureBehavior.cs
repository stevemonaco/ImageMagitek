using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace TileShop.WPF.Behaviors;

/// <summary>
/// Behavior to enable mouse capturing and notification to the ViewModel
/// </summary>
/// <remarks>
/// Implementation from Mark Feldman https://stackoverflow.com/questions/34984093/mouse-position-with-respect-to-image-in-wpf-using-mvvm
/// and modified slightly for mouse wheel behavior
/// </remarks>
public class MouseCaptureBehavior : Behavior<FrameworkElement>
{
    public static readonly DependencyProperty ProxyProperty = DependencyProperty.RegisterAttached(
        "Proxy",
        typeof(IMouseCaptureProxy),
        typeof(MouseCaptureBehavior),
        new PropertyMetadata(null, OnProxyChanged));

    public static void SetProxy(DependencyObject source, IMouseCaptureProxy value)
    {
        source.SetValue(ProxyProperty, value);
    }

    public static IMouseCaptureProxy GetProxy(DependencyObject source)
    {
        return (IMouseCaptureProxy)source.GetValue(ProxyProperty);
    }

    public bool ShouldCaptureMouseWheel
    {
        get { return (bool)GetValue(ShouldCaptureMouseWheelProperty); }
        set { SetValue(ShouldCaptureMouseWheelProperty, value); }
    }

    public static readonly DependencyProperty ShouldCaptureMouseWheelProperty =
        DependencyProperty.Register(nameof(ShouldCaptureMouseWheel), typeof(bool), typeof(MouseCaptureBehavior), new PropertyMetadata(false));

    public bool RequireCtrlForMouseWheel
    {
        get { return (bool)GetValue(RequireCtrlForMouseWheelProperty); }
        set { SetValue(RequireCtrlForMouseWheelProperty, value); }
    }

    // Using a DependencyProperty as the backing store for RequireCtrlForMouseWheel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty RequireCtrlForMouseWheelProperty =
        DependencyProperty.RegisterAttached(nameof(RequireCtrlForMouseWheel), typeof(bool), typeof(MouseCaptureBehavior));

    private static void OnProxyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is IMouseCaptureProxy oldValueProxy)
        {
            oldValueProxy.Capture -= OnCapture;
            oldValueProxy.Release -= OnRelease;
        }

        if (e.NewValue is IMouseCaptureProxy newValueProxy)
        {
            newValueProxy.Capture += OnCapture;
            newValueProxy.Release += OnRelease;
        }
    }

    static void OnCapture(object sender, EventArgs e)
    {
        if (sender is MouseCaptureBehavior behavior)
            behavior.AssociatedObject.CaptureMouse();
    }

    static void OnRelease(object sender, EventArgs e)
    {
        if (sender is MouseCaptureBehavior behavior)
            behavior.AssociatedObject.ReleaseMouseCapture();
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewMouseDown += OnMouseDown;
        AssociatedObject.PreviewMouseMove += OnMouseMove;
        AssociatedObject.PreviewMouseUp += OnMouseUp;
        AssociatedObject.MouseLeave += OnMouseLeave;

        if (ShouldCaptureMouseWheel)
            AssociatedObject.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PreviewMouseDown -= OnMouseDown;
        AssociatedObject.PreviewMouseMove -= OnMouseMove;
        AssociatedObject.PreviewMouseUp -= OnMouseUp;
        AssociatedObject.MouseLeave -= OnMouseLeave;

        if (ShouldCaptureMouseWheel)
            AssociatedObject.PreviewMouseWheel -= OnPreviewMouseWheel;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var proxy = GetProxy(this);
        if (proxy != null)
        {
            var pos = e.GetPosition(AssociatedObject);
            var args = new MouseCaptureArgs
            {
                X = pos.X,
                Y = pos.Y,
                LeftButton = (e.LeftButton == MouseButtonState.Pressed),
                RightButton = (e.RightButton == MouseButtonState.Pressed)
            };
            proxy.OnMouseDown(this, args);
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var proxy = GetProxy(this);
        if (proxy != null)
        {
            var pos = e.GetPosition(AssociatedObject);
            var args = new MouseCaptureArgs
            {
                X = pos.X,
                Y = pos.Y,
                LeftButton = (e.LeftButton == MouseButtonState.Pressed),
                RightButton = (e.RightButton == MouseButtonState.Pressed)
            };
            proxy.OnMouseMove(this, args);
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var proxy = GetProxy(this);
        if (proxy != null)
        {
            var pos = e.GetPosition(AssociatedObject);
            var args = new MouseCaptureArgs
            {
                X = pos.X,
                Y = pos.Y,
                LeftButton = (e.LeftButton == MouseButtonState.Pressed),
                RightButton = (e.RightButton == MouseButtonState.Pressed)
            };
            proxy.OnMouseUp(this, args);
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        var proxy = GetProxy(this);
        if (proxy != null)
        {
            var pos = e.GetPosition(AssociatedObject);
            var args = new MouseCaptureArgs
            {
                X = pos.X,
                Y = pos.Y,
                LeftButton = (e.LeftButton == MouseButtonState.Pressed),
                RightButton = (e.RightButton == MouseButtonState.Pressed)
            };
            proxy.OnMouseLeave(this, args);
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!ShouldCaptureMouseWheel)
            return;

        var proxy = GetProxy(this);
        if (proxy != null && e.Delta != 0)
        {
            if (!RequireCtrlForMouseWheel || (RequireCtrlForMouseWheel && Keyboard.Modifiers == ModifierKeys.Control))
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
            }
        }
    }
}
