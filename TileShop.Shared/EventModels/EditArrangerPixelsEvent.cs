using ImageMagitek;
using System;
using System.Collections.Generic;
using System.Text;
using TileShop.Shared.Models;

namespace TileShop.Shared.EventModels
{
    public class EditArrangerPixelsEvent
    {
        /// <summary>
        /// Arranger to be copied from
        /// </summary>
        public Arranger Arranger { get; set; }

        /// <summary>
        /// Original source arranger loaded in project
        /// </summary>
        public Arranger ProjectArranger { get; set; }

        /// <summary>
        /// Left edge of arranger subsection in pixels
        /// </summary>
        public int X { get; set; }
        /// <summary>
        /// Top edge of arranger subsection in pixels
        /// </summary>
        public int Y { get; set; }
        /// <summary>
        /// Width of arranger subsection in pixels
        /// </summary>
        public int Width { get; set; }
        /// <summary>
        /// Height of arranger subsection in pixels
        /// </summary>
        public int Height { get; set; }

        public EditArrangerPixelsEvent(Arranger arranger, Arranger projectArranger, int x, int y, int width, int height)
        {
            Arranger = arranger;
            ProjectArranger = projectArranger;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}
