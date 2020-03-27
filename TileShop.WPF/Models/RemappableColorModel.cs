using System.Windows.Media;
using Stylet;

namespace TileShop.WPF.Models
{
    public class RemappableColorModel : PropertyChangedBase
    {
        private int _index;
        public int Index
        {
            get => _index;
            set => SetAndNotify(ref _index, value);
        }

        private Color _color;
        public Color Color
        {
            get => _color;
            set => SetAndNotify(ref _color, value);
        }

        public RemappableColorModel(Color color, int index)
        {
            Color = color;
            Index = index;
        }
    }
}
