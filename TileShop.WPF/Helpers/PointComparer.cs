using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text;

namespace TileShop.WPF.Helpers
{
    public class PointComparer : IEqualityComparer<Point>
    {
        public bool Equals(Point a, Point b) =>
            a.X == b.X && a.Y == b.Y;

        public int GetHashCode(Point point)
        {
            unchecked
            {
                return (point.Y << 16) ^ point.X;
            }
        }
    }
}
