using System.Drawing;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.AvaloniaUI.Imaging;

public abstract class BitmapAdapter : ObservableObject
{
    private WriteableBitmap _bitmap = null!;
    public WriteableBitmap Bitmap
    {
        get => _bitmap;
        protected set => SetProperty(ref _bitmap, value);
    }

    private int _width;
    public int Width
    {
        get => _width;
        protected set => SetProperty(ref _width, value);
    }

    private int _height;
    public int Height
    {
        get => _height;
        protected set => SetProperty(ref _height, value);
    }

    public int DpiX { get; protected set; } = 96;
    public int DpiY { get; protected set; } = 96;
    public PixelFormat PixelFormat { get; protected set; } = PixelFormat.Bgra8888;

    public abstract void Invalidate();
    public abstract void Invalidate(Rectangle redrawRect);
    public abstract void Invalidate(int x, int y, int width, int height);

    protected abstract void Render(int x, int y, int width, int height);
}
