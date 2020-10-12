using ImageMagitek;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Drawing;
using TileShop.Shared.Models;
using TileShop.WPF.Imaging;

namespace TileShop.WPF.Models
{
    public class ArrangerPaste : INotifyPropertyChanged
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
                SetField(ref _snapMode, value);
                if (Rect is object)
                    Rect.SnapMode = value;
            }
        }

        private BitmapAdapter _overlayImage;
        public BitmapAdapter OverlayImage
        {
            get => _overlayImage;
            private set => SetField(ref _overlayImage, value);
        }

        private SnappedRectangle _rect;
        public SnappedRectangle Rect
        {
            get => _rect;
            set => SetField(ref _rect, value);
        }

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
                else if (elementCopy.Source.ColorType == PixelColorType.Direct)
                {
                    var image = new DirectImage(elementCopy.Source, x, y, width, height);
                    OverlayImage = new DirectBitmapAdapter(image);
                }
            }
            else if (copy is IndexedPixelCopy ipc)
            {
                OverlayImage = new IndexedBitmapAdapter(ipc.Image);
            }
            else if (copy is DirectPixelCopy dpc)
            {
                OverlayImage = new DirectBitmapAdapter(dpc.Image);
            }

            Rect = new SnappedRectangle(new Size(OverlayImage.Width, OverlayImage.Height), copy.Source.ElementPixelSize, SnapMode, ElementSnapRounding.Floor);
            Rect.SetBounds(0, OverlayImage.Width, 0, OverlayImage.Height);
        }

        public void MoveTo(int x, int y) => Rect.MoveTo(x - DeltaX, y - DeltaY);

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
