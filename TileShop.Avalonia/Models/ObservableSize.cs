using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.AvaloniaUI.Models;
public sealed partial class ObservableSize : ObservableObject
{
    [ObservableProperty] private int _width;
    [ObservableProperty] private int _height;

    public ObservableSize(int width, int height)
    {
        _width = width;
        _height = height;
    }
}
