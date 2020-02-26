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
        /// <summary>
        /// Arranger to be copied from
        /// </summary>
        public Arranger Arranger { get; set; }

        /// <summary>
        /// Left edge of arranger in elements/pixels
        /// </summary>
        public int X { get; set; }
        /// <summary>
        /// Top edge of arranger in elements/pixels
        /// </summary>
        public int Y { get; set; }
        /// <summary>
        /// Width of arranger in elements/pixels
        /// </summary>
        public int Width { get; set; }
        /// <summary>
        /// Height of arranger in elements/pixels
        /// </summary>
        public int Height { get; set; }

        public ArrangerTransferModel() { }

        /// <summary>
        /// Model to transfer a subsection of an Arranger
        /// </summary>
        /// <param name="arranger">Arranger to be copied from</param>
        /// <param name="x">Left edge of arranger in elements/pixels</param>
        /// <param name="y">Top edge of arranger in elements/pixels</param>
        /// <param name="width">Width of arranger in elements/pixels</param>
        /// <param name="height">Height of arranger in elements/pixels</param>
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
