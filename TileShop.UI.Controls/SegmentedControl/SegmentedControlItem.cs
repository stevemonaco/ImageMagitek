using Avalonia;
using Avalonia.Controls;

namespace TileShop.UI.Controls;

public class SegmentedControlItem : ContentControl
{
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<SegmentedControlItem, bool>(nameof(IsSelected));

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    static SegmentedControlItem()
    {
        IsSelectedProperty.Changed.AddClassHandler<SegmentedControlItem>((item, _) =>
            item.PseudoClasses.Set(":selected", item.IsSelected));
    }
}
