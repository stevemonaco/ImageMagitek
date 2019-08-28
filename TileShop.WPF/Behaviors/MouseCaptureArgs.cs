using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.WPF.Behaviors
{
    public class MouseCaptureArgs
    {
        public double X { get; set; }
        public double Y { get; set; }
        public bool LeftButton { get; set; }
        public bool RightButton { get; set; }
    }
}
