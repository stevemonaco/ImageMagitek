﻿using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.WPF.Models
{
    public class Gridline
    {
        public Gridline() { }
        public Gridline(int x1, int y1, int x2, int y2)
        {
            X1 = x1;
            Y1 = y1;

            X2 = x2;
            Y2 = y2;
        }

        public double X1 { get; set; }
        public double X2 { get; set; }
        public double Y1 { get; set; }
        public double Y2 { get; set; }
    }
}
