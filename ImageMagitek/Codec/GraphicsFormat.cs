using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using System.Xml.Linq;

namespace ImageMagitek
{
    /// <summary>
    /// Specifies how the graphical viewer will treat the graphic
    /// Tiled graphics will render a grid of multiple images
    /// Linear graphics will render a single image
    /// </summary>
    public enum ImageLayout { Tiled = 0, Linear }

    /// <summary>
    /// Specifies how the pixels' colors are determined for the graphic
    /// Indexed graphics have their full color determined by a palette
    /// Direct graphics have their full color determined by the pixel image data alone
    /// </summary>
    public enum PixelColorType { Indexed = 0, Direct }

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
        ///   Ex: [3, 2, 0, 1] implies the first bit read will merge into bit 3,
        ///   second bit read into bit 2, third bit read into bit 0, fourth bit read into bit 1
        /// </summary>
        public int[] MergePriority { get; set; }

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
        public int StorageSize(int width, int height) { return (width + RowStride) * height * ColorDepth + ElementStride; }

        public IList<ImageProperty> ImageProperties { get; set; } = new List<ImageProperty>();

        // Processing Operations
        public bool HFlip { get; set; }
        public bool VFlip { get; set; }
        public bool Remap { get; set; }

        // Pixel remap operations (TODO)

        public GraphicsFormat() { }

        public void Resize(int width, int height)
        {
            if (width != Width || height != Height)
            {
                Height = height;
                Width = width;

                for (int i = 0; i < ImageProperties.Count; i++)
                    ImageProperties[i].ExtendRowPattern(Width);
            }
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
            clone.HFlip = HFlip;
            clone.VFlip = VFlip;
            clone.Remap = Remap;

            clone.MergePriority = new int[MergePriority.Length];
            Array.Copy(MergePriority, clone.MergePriority, MergePriority.Length);

            clone.ImageProperties = new List<ImageProperty>();
            foreach(var prop in ImageProperties)
            {
                var pattern = new int[prop.RowPixelPattern.Length];
                Array.Copy(prop.RowPixelPattern, pattern, prop.RowPixelPattern.Length);

                var propclone = new ImageProperty(prop.ColorDepth, prop.RowInterlace, pattern);
                clone.ImageProperties.Add(propclone);
            }

            return clone;
        }
    }
}
