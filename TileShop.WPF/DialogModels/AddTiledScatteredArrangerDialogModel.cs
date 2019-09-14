using Caliburn.Micro;

namespace TileShop.WPF.DialogModels
{
    public class AddTiledScatteredArrangerDialogModel : PropertyChangedBase
    {
        private string _arrangerName;
        public string ArrangerName
        {
            get => _arrangerName;
            set => Set(ref _arrangerName, value);
        }

        private int _arrangerElementWidth;
        public int ArrangerElementWidth
        {
            get => _arrangerElementWidth;
            set => Set(ref _arrangerElementWidth, value);
        }

        private int _arrangerElementHeight;
        public int ArrangerElementHeight
        {
            get => _arrangerElementHeight;
            set => Set(ref _arrangerElementHeight, value);
        }

        private int _elementPixelWidth;
        public int ElementPixelWidth
        {
            get => _elementPixelWidth;
            set => Set(ref _elementPixelWidth, value);
        }

        private int _elementPixelHeight;
        public int ElementPixelHeight
        {
            get => _elementPixelHeight;
            set => Set(ref _elementPixelHeight, value);
        }
    }
}
