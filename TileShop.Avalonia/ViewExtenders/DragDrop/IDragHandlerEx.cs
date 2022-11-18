using Avalonia.Xaml.Interactions.DragAndDrop;

namespace TileShop.AvaloniaUI.DragDrop;
public interface IDragHandlerEx : IDragHandler
{
    public object? Payload { get; set; }
}
