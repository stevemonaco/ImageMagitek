using System.Collections.Generic;
using System.Drawing;

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
