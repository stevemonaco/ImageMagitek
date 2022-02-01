using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.AvaloniaUI.Models;

public partial class RemappableColorModel : ObservableObject
{
    [ObservableProperty] private int _index;
    [ObservableProperty] private Color _color;

    public RemappableColorModel(Color color, int index)
    {
        Color = color;
        Index = index;
    }
}
