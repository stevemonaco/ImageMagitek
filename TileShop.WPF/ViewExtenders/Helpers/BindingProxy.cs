using System.Windows;

namespace TileShop.WPF.Helpers;

// Source: http://www.thomaslevesque.com/2011/03/21/wpf-how-to-bind-to-data-when-the-datacontext-is-not-inherited/
public sealed class BindingProxy : Freezable
{
    /// <summary>Identifies the <see cref="Data"/> dependency property.</summary>
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data),
        typeof(object),
        typeof(BindingProxy),
        new UIPropertyMetadata(default));

    protected override Freezable CreateInstanceCore()
    {
        return new BindingProxy();
    }

    public object Data
    {
        get { return GetValue(DataProperty); }
        set { SetValue(DataProperty, value); }
    }
}
