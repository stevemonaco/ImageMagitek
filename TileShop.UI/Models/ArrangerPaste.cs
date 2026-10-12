using System;
using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using TileShop.UI.DragDrop;
using TileShop.UI.Features.Graphics;
using TileShop.Shared.Models;

namespace TileShop.UI.Models;

public partial class ArrangerPaste : ObservableObject, IDraggable
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

    [ObservableProperty] private ArrangerSkiaBitmap _overlayImage;
    [ObservableProperty] private SnappedRectangle _rect;
    [ObservableProperty] private bool _isDragging;

    public ArrangerPaste(ArrangerCopy copy, SnapMode snapMode)
    {
        Copy = copy;
        SnapMode = snapMode;

        var pixelCopy = copy is ElementCopy elementCopy ? elementCopy.ToPixelCopy() : copy;
        _overlayImage = pixelCopy switch
        {
            IndexedPixelCopy ipc => new ArrangerSkiaBitmap(ipc.Image),
            DirectPixelCopy dpc => new ArrangerSkiaBitmap(dpc.Image),
            _ => throw new NotSupportedException($"{nameof(ArrangerPaste)}: Copy of type '{copy.GetType()}' is not supported")
        };

        _rect = new SnappedRectangle(new Size(OverlayImage.Width, OverlayImage.Height), copy.ElementPixelSize, SnapMode, ElementSnapRounding.Floor);
        _rect.SetBounds(0, OverlayImage.Width, 0, OverlayImage.Height);
    }

    public void MoveTo(int x, int y) => Rect.MoveTo(x - DeltaX, y - DeltaY);
}
