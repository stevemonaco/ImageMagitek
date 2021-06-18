using System.Windows.Media;
using ImageMagitek.Colors;
using Stylet;

namespace TileShop.WPF.ViewModels
{
    public abstract class EditableColorBaseViewModel : Screen
    {
        public abstract bool CanSaveColor { get; }
        public IColor WorkingColor { get; set; }

        private Color _color;
        public Color Color
        {
            get => _color;
            set => SetAndNotify(ref _color, value);
        }

        protected int _index;
        public int Index
        {
            get => _index;
            set => SetAndNotify(ref _index, value);
        }
    }
}
