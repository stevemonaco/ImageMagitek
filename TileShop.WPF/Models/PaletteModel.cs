using Stylet;
using ImageMagitek.Colors;
using System.Windows.Media;

namespace TileShop.WPF.Models
{
    public class PaletteModel : PropertyChangedBase
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => SetAndNotify(ref _name, value);
        }

        private BindableCollection<Color> _colors = new BindableCollection<Color>();
        public BindableCollection<Color> Colors
        {
            get => _colors;
            set => SetAndNotify(ref _colors, value);
        }

        public static PaletteModel FromArrangerPalette(Palette pal) => FromArrangerPalette(pal, pal.Entries);

        public static PaletteModel FromArrangerPalette(Palette pal, int colorCount)
        {
            var model = new PaletteModel();
            model.Name = pal.Name;

            for (int i = 0; i < colorCount; i++)
            {
                var color = new Color();
                color.R = pal[i].R;
                color.G = pal[i].G;
                color.B = pal[i].B;
                color.A = pal[i].A;

                model.Colors.Add(color);
            }

            return model;
        }

    }
}
