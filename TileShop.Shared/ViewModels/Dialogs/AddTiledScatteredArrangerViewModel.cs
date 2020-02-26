using Stylet;

namespace TileShop.Shared.ViewModels
{
    public class AddTiledScatteredArrangerViewModel : Screen
    {
        private string _arrangerName;
        public string ArrangerName
        {
            get => _arrangerName;
            set => SetAndNotify(ref _arrangerName, value);
        }

        private int _arrangerElementWidth;
        public int ArrangerElementWidth
        {
            get => _arrangerElementWidth;
            set => SetAndNotify(ref _arrangerElementWidth, value);
        }

        private int _arrangerElementHeight;
        public int ArrangerElementHeight
        {
            get => _arrangerElementHeight;
            set => SetAndNotify(ref _arrangerElementHeight, value);
        }

        private int _elementPixelWidth;
        public int ElementPixelWidth
        {
            get => _elementPixelWidth;
            set => SetAndNotify(ref _elementPixelWidth, value);
        }

        private int _elementPixelHeight;
        public int ElementPixelHeight
        {
            get => _elementPixelHeight;
            set => SetAndNotify(ref _elementPixelHeight, value);
        }
    }
}
