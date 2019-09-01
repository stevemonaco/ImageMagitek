using ImageMagitek;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;

namespace TileShop.WPF.Models
{
    public class ArrangerTransferModel
    {
        public Arranger Arranger { get; set; }

        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public ArrangerTransferModel() { }

        public ArrangerTransferModel(Arranger arranger, int x, int y, int width, int height)
        {
            Arranger = arranger;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}
