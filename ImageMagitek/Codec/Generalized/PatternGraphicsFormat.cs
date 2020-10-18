using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Codec
{
    public class PatternGraphicsFormat : IGraphicsFormat
    {
        public PatternList Pattern { get; protected set; }

        /// <summary>
        /// The name of the codec
        /// </summary>
        public string Name { get; }

        public int ColorDepth { get; }
        public PixelColorType ColorType { get; }
        public ImageLayout Layout { get; }

        public int DefaultWidth { get; }
        public int DefaultHeight { get; }
        public int Width => DefaultWidth;
        public int Height => DefaultHeight;

        public bool FixedSize => true;
        public int StorageSize => Pattern?.PatternSize ?? 0;

        public PatternGraphicsFormat(string name, PixelColorType colorType, int colorDepth, 
            ImageLayout layout, int defaultWidth, int defaultHeight)
        {
            Name = name;
            ColorType = colorType;
            ColorDepth = colorDepth;
            Layout = layout;
            DefaultWidth = defaultWidth;
            DefaultHeight = defaultHeight;
        }

        public IGraphicsFormat Clone()
        {
            var format = new PatternGraphicsFormat(Name, ColorType, ColorDepth, Layout, DefaultWidth, DefaultHeight);
            format.SetPattern(Pattern);

            return format;
        }

        public void SetPattern(PatternList pattern)
        {
            Pattern = pattern;
        }
    }
}
