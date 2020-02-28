using ImageMagitek;
using Stylet;

namespace TileShop.WPF.ViewModels
{
    public class AddScatteredArrangerViewModel : Screen
    {
        private string _arrangerName;
        public string ArrangerName
        {
            get => _arrangerName;
            set => SetAndNotify(ref _arrangerName, value);
        }

        private PixelColorType _colorType = PixelColorType.Indexed;
        public PixelColorType ColorType
        {
            get => _colorType;
            set => SetAndNotify(ref _colorType, value);
        }

        private ArrangerLayout _layout = ArrangerLayout.Tiled;
        public ArrangerLayout Layout
        {
            get => _layout;
            set => SetAndNotify(ref _layout, value);
        }

        private int _arrangerElementWidth = 8;
        public int ArrangerElementWidth
        {
            get => _arrangerElementWidth;
            set => SetAndNotify(ref _arrangerElementWidth, value);
        }

        private int _arrangerElementHeight = 16;
        public int ArrangerElementHeight
        {
            get => _arrangerElementHeight;
            set => SetAndNotify(ref _arrangerElementHeight, value);
        }

        private int _elementPixelWidth = 8;
        public int ElementPixelWidth
        {
            get => _elementPixelWidth;
            set => SetAndNotify(ref _elementPixelWidth, value);
        }

        private int _elementPixelHeight = 8;
        public int ElementPixelHeight
        {
            get => _elementPixelHeight;
            set => SetAndNotify(ref _elementPixelHeight, value);
        }

        public void Add()
        {
            if (Layout == ArrangerLayout.Single)
            {
                ArrangerElementHeight = 1;
                ArrangerElementWidth = 1;
            }

            RequestClose(true);
        }

        public void Cancel() => RequestClose(false);
    }
}
