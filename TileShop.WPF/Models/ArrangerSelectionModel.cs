using ImageMagitek;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace TileShop.WPF.Models
{
    public class ArrangerSelectionModel
    {
        /// <summary>
        /// Arranger which holds the data to be copied
        /// </summary>
        public Arranger Arranger { get; private set; }

        /// <summary>
        /// Upper left location of the selection in pixels
        /// </summary>
        public Point Location { get; private set; }

        /// <summary>
        /// Size of selection in pixels
        /// </summary>
        public Size SelectionSize { get; private set; }
    }
}
