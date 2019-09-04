using ImageMagitek;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;

namespace TileShop.Shared.Models
{
    public class ArrangerTransferModel
    {
        public Arranger Arranger { get; set; }

        /// <summary>
        /// Left edge of arranger in pixels
        /// </summary>
        public int X { get; set; }
        /// <summary>
        /// Left edge of arranger in pixels
        /// </summary>
        public int Y { get; set; }
        /// <summary>
        /// Width of arranger in pixels
        /// </summary>
        public int Width { get; set; }
        /// <summary>
        /// Height of arranger in pixels
        /// </summary>
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
