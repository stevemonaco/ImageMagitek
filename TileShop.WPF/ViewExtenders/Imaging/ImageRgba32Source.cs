using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Advanced;

namespace TileShop.WPF.Imaging;

/// <summary>
/// Adapts ImageSharp Rgba32 to BitmapSource for WPF
/// </summary>
/// <remarks>
/// Implementation based upon: https://github.com/jongleur1983/SharpImageSource/blob/master/ImageSharp.WpfImageSource/ImageSharpImageSource.cs
/// Reference: http://www.i-programmer.info/programming/wpf-workings/822
/// Reference: https://blogs.msdn.microsoft.com/dwayneneed/2008/06/20/implementing-a-custom-bitmapsource/
/// </remarks>
public class ImageRgba32Source : BitmapSourceBase
{
    private Image<Rgba32> _source;

    public ImageRgba32Source(Image<Rgba32> source)
    {
        _source = source;
        PixelWidth = _source.Width;
        PixelHeight = _source.Height;
    }

    public ImageRgba32Source(int width, int height)
    {
        _source = new Image<Rgba32>(width, height);
    }

    protected override Freezable CreateInstanceCore() => new ImageRgba32Source(16, 16);

    public override PixelFormat Format => PixelFormats.Bgra32;
    public override int PixelWidth { get; }
    public override int PixelHeight { get; }
    public override double DpiX => _source.Metadata.HorizontalResolution;
    public override double DpiY => _source.Metadata.VerticalResolution;
    public override BitmapPalette Palette => null;

    protected override void CopyPixelsCore(Int32Rect sourceRect, int stride, int bufferSize, IntPtr buffer)
    {
        if (_source is not null)
        {
            unsafe
            {
                byte* pBytes = (byte*)buffer.ToPointer();
                for (int y = 0; y < sourceRect.Height; y++)
                {
                    var row = _source.GetPixelRowSpan(y);

                    for (int x = 0; x < sourceRect.Width; x++)
                    {
                        pBytes[x * 4] = row[x].B;
                        pBytes[x * 4 + 1] = row[x].G;
                        pBytes[x * 4 + 2] = row[x].R;
                        pBytes[x * 4 + 3] = row[x].A;
                    }

                    pBytes += stride;
                }
            }
        }
    }
}
