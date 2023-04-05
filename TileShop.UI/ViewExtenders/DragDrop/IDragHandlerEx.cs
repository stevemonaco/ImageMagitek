using Avalonia.Xaml.Interactions.DragAndDrop;

namespace TileShop.UI.DragDrop;
public interface IDragHandlerEx : IDragHandler
{
    public object? Payload { get; set; }
}
