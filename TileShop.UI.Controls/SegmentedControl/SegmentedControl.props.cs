using System.Collections;
using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using Avalonia.Styling;

namespace TileShop.UI.Controls;

public partial class SegmentedControl
{
    public static readonly StyledProperty<ControlTheme?> ItemContainerThemeProperty =
        AvaloniaProperty.Register<SegmentedControl, ControlTheme?>(nameof(ItemContainerTheme));

    public ControlTheme? ItemContainerTheme
    {
        get => GetValue(ItemContainerThemeProperty);
        set => SetValue(ItemContainerThemeProperty, value);
    }

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<SegmentedControl, IEnumerable?>(nameof(ItemsSource));

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<SegmentedControl, IDataTemplate?>(nameof(ItemTemplate));

    [InheritDataTypeFromItems(nameof(ItemsSource))]
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<SegmentedControl, object?>(nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<SegmentedControl, int>(nameof(SelectedIndex), defaultValue: -1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }
}
