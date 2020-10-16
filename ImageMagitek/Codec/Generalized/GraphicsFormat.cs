using System;
using System.Collections.Generic;

namespace ImageMagitek.Codec
{
    /// <summary>
    /// GraphicsFormat describes properties relating to decoding/encoding a general graphics format
    /// </summary>
    public class GraphicsFormat
    {
        /// <summary>
        /// The name of the codec
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Returns true if the codec requires fixed size elements or false if the codec operates on variable size elements
        /// </summary>
        public bool FixedSize { get; set; }

        /// <summary>
        /// Specifies if the graphic is rendered and manipulated as a tiled grid or not
        /// </summary>
        public ImageLayout Layout { get; set; }

        /// <summary>
        /// The color depth of the format in bits per pixel
        /// </summary>
        public int ColorDepth { get; set; }

        /// <summary>
        /// ColorType defines how pixel data is translated into color data
        /// </summary>
        public PixelColorType ColorType { get; set; }

        /// <summary>
        /// Specifies how individual bits of each color are merged according to priority
        ///   Ex: [3, 2, 0, 1] implies the first bit read will merge into plane 3,
        ///   second bit read into plane 2, third bit read into plane 0, fourth bit read into plane 1
        /// </summary>
        public int[] MergePlanePriority { get; set; }

        /// <summary>
        /// Current width of the elements to encode/decode
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Current height of elements to encode/decode
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Default width of an element as specified by the XML file
        /// </summary>
        public int DefaultWidth { get; set; }

        /// <summary>
        /// Default height of an element as specified by the XML file
        /// </summary>
        public int DefaultHeight { get; set; }

        /// <summary>
        /// Number of bits to skip after each row
        /// </summary>
        public int RowStride { get; set; }

        /// <summary>
        /// Number of bits to skip after each element
        /// </summary>
        public int ElementStride { get; set; }

        /// <summary>
        /// Storage size of an element in bits
        /// </summary>
        /// <returns></returns>
        public int StorageSize => (Width + RowStride) * Height * ColorDepth + ElementStride;

        public IList<ImageProperty> ImageProperties { get; set; } = new List<ImageProperty>();

        public GraphicsFormat() { }

        public void Resize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public GraphicsFormat Clone()
        {
            var clone = new GraphicsFormat();
            clone.Name = Name;
            clone.FixedSize = FixedSize;
            clone.Layout = Layout;
            clone.ColorDepth = ColorDepth;
            clone.ColorType = ColorType;
            clone.Width = Width;
            clone.Height = Height;
            clone.DefaultWidth = DefaultWidth;
            clone.DefaultHeight = DefaultHeight;
            clone.RowStride = RowStride;
            clone.ElementStride = ElementStride;

            clone.MergePlanePriority = new int[MergePlanePriority.Length];
            Array.Copy(MergePlanePriority, clone.MergePlanePriority, MergePlanePriority.Length);

            clone.ImageProperties = new List<ImageProperty>();
            foreach(var prop in ImageProperties)
            {
                var pattern = new BroadcastList<int>(prop.RowPixelPattern);

                var propclone = new ImageProperty(prop.ColorDepth, prop.RowInterlace, pattern);
                clone.ImageProperties.Add(propclone);
            }

            return clone;
        }
    }
}
