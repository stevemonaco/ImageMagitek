using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interactivity;

namespace TileShop.WPF.Behaviors
{
    /// <summary>
    /// Behavior to enable mouse capturing and notification to the ViewModel
    /// </summary>
    /// <remarks>
    /// Implementation from Mark Feldman https://stackoverflow.com/questions/34984093/mouse-position-with-respect-to-image-in-wpf-using-mvvm
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

        private static void OnProxyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is IMouseCaptureProxy)
            {
                (e.OldValue as IMouseCaptureProxy).Capture -= OnCapture;
                (e.OldValue as IMouseCaptureProxy).Release -= OnRelease;
            }
            if (e.NewValue is IMouseCaptureProxy)
            {
                (e.NewValue as IMouseCaptureProxy).Capture += OnCapture;
                (e.NewValue as IMouseCaptureProxy).Release += OnRelease;
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
            this.AssociatedObject.PreviewMouseDown += OnMouseDown;
            this.AssociatedObject.PreviewMouseMove += OnMouseMove;
            this.AssociatedObject.PreviewMouseUp += OnMouseUp;
            this.AssociatedObject.MouseLeave += OnMouseLeave;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            this.AssociatedObject.PreviewMouseDown -= OnMouseDown;
            this.AssociatedObject.PreviewMouseMove -= OnMouseMove;
            this.AssociatedObject.PreviewMouseUp -= OnMouseUp;
            this.AssociatedObject.MouseLeave -= OnMouseLeave;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var proxy = GetProxy(this);
            if (proxy != null)
            {
                var pos = e.GetPosition(this.AssociatedObject);
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
                var pos = e.GetPosition(this.AssociatedObject);
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
                var pos = e.GetPosition(this.AssociatedObject);
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
                var pos = e.GetPosition(this.AssociatedObject);
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

    }
}
