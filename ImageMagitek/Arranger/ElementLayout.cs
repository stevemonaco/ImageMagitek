using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ImageMagitek
{
    /// <summary>
    /// Defines a rectangular element layout for Sequential Arrangers
    /// </summary>
    public sealed class ElementLayout
    {
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public int TilesPerPattern { get; }
        public IEnumerable<Point> Pattern => _pattern;

        private List<Point> _pattern;

        public ElementLayout(string name, int width, int height, int tilesPerPattern, IEnumerable<Point> pattern)
        {
            Name = name;
            Width = width;
            Height = height;
            TilesPerPattern = tilesPerPattern;
            _pattern = pattern.ToList();
        }

        public static ElementLayout Default { get; } = new ElementLayout("Default", 1, 1, 1, new Point[] { new Point(0, 0) });
    }
}
