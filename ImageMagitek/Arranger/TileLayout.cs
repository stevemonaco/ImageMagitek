using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMagitek
{
    /// <summary>
    /// Defines a rectangular tile layout for Sequential Arrangers
    /// </summary>
    public class TileLayout
    {
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public int TilesPerPattern { get; }
        public IEnumerable<LayoutElement> Pattern => _pattern;

        private List<LayoutElement> _pattern;

        public TileLayout(string name, int width, int height, int tilesPerPattern, IEnumerable<LayoutElement> pattern)
        {
            Name = name;
            Width = width;
            Height = height;
            TilesPerPattern = tilesPerPattern;
            _pattern = pattern.ToList();
        }
    }
}
