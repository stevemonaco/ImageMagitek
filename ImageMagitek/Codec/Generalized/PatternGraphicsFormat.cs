using System.Linq;

namespace ImageMagitek.Codec
{
    public enum PixelPacking { Planar, Chunky }

    public sealed class PatternGraphicsFormat : IGraphicsFormat
    {
        public PatternList Pattern { get; private set; }

        /// <summary>
        /// The name of the codec
        /// </summary>
        public string Name { get; }

        public int ColorDepth { get; }
        public PixelColorType ColorType { get; }
        public ImageLayout Layout { get; }
        public PixelPacking Packing { get; }

        /// <summary>
        /// Specifies how individual bits of each color are merged according to priority
        ///   Ex: [3, 2, 0, 1] implies the first bit read will merge into plane 3,
        ///   second bit read into plane 2, third bit read into plane 0, fourth bit read into plane 1
        /// </summary>
        public int[] MergePlanePriority { get; set; }

        public RepeatList RowPixelPattern { get; set; }

        public int DefaultWidth { get; }
        public int DefaultHeight { get; }
        public int Width => DefaultWidth;
        public int Height => DefaultHeight;

        public bool FixedSize => true;
        public int StorageSize => Width * Height * ColorDepth;

        public PatternGraphicsFormat(string name, PixelColorType colorType, int colorDepth, 
            ImageLayout layout, PixelPacking packing, int defaultWidth, int defaultHeight)
        {
            Name = name;
            ColorType = colorType;
            ColorDepth = colorDepth;
            Layout = layout;
            Packing = packing;
            DefaultWidth = defaultWidth;
            DefaultHeight = defaultHeight;
        }

        public IGraphicsFormat Clone()
        {
            var format = new PatternGraphicsFormat(Name, ColorType, ColorDepth, Layout, Packing, DefaultWidth, DefaultHeight);
            format.SetPattern(Pattern);
            format.MergePlanePriority = MergePlanePriority.ToArray();
            format.RowPixelPattern = new RepeatList(RowPixelPattern);

            return format;
        }

        public void SetPattern(PatternList pattern)
        {
            Pattern = pattern;
        }
    }
}
