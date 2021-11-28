using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Xaml.Behaviors;

namespace TileShop.WPF.Behaviors;

/// <summary>
/// Behavior to select all text upon focus to allow immediate overwriting by user
/// </summary>
/// <remarks>Implementation from https://www.codeproject.com/Tips/1249276/WPF-Select-All-Focus-Behavior</remarks>
public class SelectAllFocusBehavior : Behavior<TextBox>
{
    public static bool GetEnable(FrameworkElement frameworkElement) =>
        (bool)frameworkElement.GetValue(EnableProperty);

    public static void SetEnable(FrameworkElement frameworkElement, bool value) =>
        frameworkElement.SetValue(EnableProperty, value);

    public static readonly DependencyProperty EnableProperty =
             DependencyProperty.RegisterAttached("Enable",
                typeof(bool), typeof(SelectAllFocusBehavior),
                new FrameworkPropertyMetadata(false, OnEnableChanged));

    private static void OnEnableChanged
               (DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!(d is FrameworkElement frameworkElement))
            return;

        if (e.NewValue is bool == false)
            return;

        if ((bool)e.NewValue)
        {
            frameworkElement.GotFocus += SelectAll;
            frameworkElement.PreviewMouseDown += IgnoreMouseButton;
            frameworkElement.Loaded += SelectAll;
        }
        else
        {
            frameworkElement.GotFocus -= SelectAll;
            frameworkElement.PreviewMouseDown -= IgnoreMouseButton;
            frameworkElement.Loaded -= SelectAll;
        }
    }

    private static void SelectAll(object sender, RoutedEventArgs e)
    {
        var frameworkElement = e.OriginalSource as FrameworkElement;
        if (frameworkElement is TextBox)
            ((TextBoxBase)frameworkElement).SelectAll();
        else if (frameworkElement is PasswordBox)
            ((PasswordBox)frameworkElement).SelectAll();
    }

    private static void IgnoreMouseButton(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!(sender is FrameworkElement frameworkElement) || frameworkElement.IsKeyboardFocusWithin)
            return;

        e.Handled = true;
        frameworkElement.Focus();
    }
}
