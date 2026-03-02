using Avalonia;
using Avalonia.Controls;

namespace TileShop.UI.Controls;

public class SegmentItem : ContentControl
{
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<SegmentItem, bool>(nameof(IsSelected));

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    static SegmentItem()
    {
        IsSelectedProperty.Changed.AddClassHandler<SegmentItem>((item, _) =>
            item.PseudoClasses.Set(":selected", item.IsSelected));
    }
}
