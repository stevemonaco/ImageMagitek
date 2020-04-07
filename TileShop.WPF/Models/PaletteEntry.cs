using Stylet;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace TileShop.WPF.Models
{
    public class PaletteEntry : PropertyChangedBase
    {
        public byte Index { get; }
        public Color Color { get; }

        public PaletteEntry(byte index, Color color)
        {
            Index = index;
            Color = color;
        }
    }
}
