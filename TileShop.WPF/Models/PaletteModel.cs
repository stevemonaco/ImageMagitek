using Caliburn.Micro;
using ImageMagitek.Colors;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace TileShop.WPF.Models
{
    public class PaletteModel : PropertyChangedBase
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        private BindableCollection<Color> _colors = new BindableCollection<Color>();
        public BindableCollection<Color> Colors
        {
            get => _colors;
            set => Set(ref _colors, value);
        }

        public static PaletteModel FromArrangerPalette(Palette pal)
        {
            var model = new PaletteModel();
            model.Name = pal.Name;

            for(int i = 0; i < pal.Entries; i++)
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
