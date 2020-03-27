using System.Windows;

namespace TileShop.WPF.Behaviors
{
    /// <summary>
    /// WPF XAML helper for setting the appropriate DialogResult
    /// </summary>
    /// <remarks>
    /// Implementation from https://stackoverflow.com/questions/501886/how-should-the-viewmodel-close-the-form/1153602#1153602
    /// </remarks>
    public static class DialogCloser
    {
        public static readonly DependencyProperty DialogResultProperty = 
            DependencyProperty.RegisterAttached("DialogResult", typeof(bool?), typeof(DialogCloser), new PropertyMetadata(DialogResultChanged));

        public static void DialogResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if(d is Window window)
            {
                window.DialogResult = e.NewValue as bool?;
            }
        }

        public static void SetDialogResult(Window target, bool? value)
        {
            target.SetValue(DialogResultProperty, value);
        }
    }
}
