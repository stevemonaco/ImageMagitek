using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using TileShop.AvaloniaUI.Imaging;
using TileShop.Shared.Models;

namespace TileShop.AvaloniaUI.Models;

public partial class ArrangerPaste : ObservableObject
{
    public ArrangerCopy Copy { get; private set; }
    public int DeltaX { get; set; }
    public int DeltaY { get; set; }

    private SnapMode _snapMode;
    public SnapMode SnapMode
    {
        get => _snapMode;
        set
        {
            SetProperty(ref _snapMode, value);
            if (Rect is not null)
                Rect.SnapMode = value;
        }
    }

    [ObservableProperty] private BitmapAdapter _overlayImage;

    [ObservableProperty] private SnappedRectangle _rect;

    public ArrangerPaste(ArrangerCopy copy, SnapMode snapMode)
    {
        Copy = copy;
        SnapMode = snapMode;

        if (copy is ElementCopy elementCopy)
        {
            var x = elementCopy.X * elementCopy.ElementPixelWidth;
            var y = elementCopy.Y * elementCopy.ElementPixelHeight;
            var width = elementCopy.Width * elementCopy.Source.ElementPixelSize.Width;
            var height = elementCopy.Height * elementCopy.Source.ElementPixelSize.Height;

            if (elementCopy.Source.ColorType == PixelColorType.Indexed)
            {
                var image = new IndexedImage(elementCopy.Source, x, y, width, height);
                OverlayImage = new IndexedBitmapAdapter(image);
            }
            //else if (elementCopy.Source.ColorType == PixelColorType.Direct)
            //{
            //    var image = new DirectImage(elementCopy.Source, x, y, width, height);
            //    OverlayImage = new DirectBitmapAdapter(image);
            //}
        }
        else if (copy is IndexedPixelCopy ipc)
        {
            OverlayImage = new IndexedBitmapAdapter(ipc.Image);
        }
        //else if (copy is DirectPixelCopy dpc)
        //{
        //    OverlayImage = new DirectBitmapAdapter(dpc.Image);
        //}

        Rect = new SnappedRectangle(new Size(OverlayImage.Width, OverlayImage.Height), copy.Source.ElementPixelSize, SnapMode, ElementSnapRounding.Floor);
        Rect.SetBounds(0, OverlayImage.Width, 0, OverlayImage.Height);
    }

    public void MoveTo(int x, int y) => Rect.MoveTo(x - DeltaX, y - DeltaY);
}
