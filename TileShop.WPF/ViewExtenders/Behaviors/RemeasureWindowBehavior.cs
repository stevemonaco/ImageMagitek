using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace TileShop.WPF.Behaviors
{
    public class RemeasureWindowBehavior : Behavior<Window>
    {
        public bool Remeasure
        {
            get { return (bool)GetValue(RemeasureProperty); }
            set { SetValue(RemeasureProperty, value); }
        }

        public static bool GetRemeasure(FrameworkElement frameworkElement) =>
            (bool)frameworkElement.GetValue(RemeasureProperty);

        public static void SetRemeasure(FrameworkElement frameworkElement, bool value) =>
            frameworkElement.SetValue(RemeasureProperty, value);

        public static readonly DependencyProperty RemeasureProperty =
            DependencyProperty.RegisterAttached(nameof(Remeasure), typeof(bool), typeof(RemeasureWindowBehavior), new PropertyMetadata(false, OnRemeasureChanged));

        private static void OnRemeasureChanged
           (DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is Window window))
                return;

            if (e.NewValue is bool == false)
                return;

            if ((bool)e.NewValue)
            {
                window.ContentRendered += Window_ContentRendered;
            }
            else
            {
                window.ContentRendered -= Window_ContentRendered;
            }
        }

        private static void Window_ContentRendered(object sender, System.EventArgs e)
        {
            if (sender is Window window)
            {
                window.InvalidateMeasure();
                window.ContentRendered -= Window_ContentRendered;
            }
        }
    }
}
