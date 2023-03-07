using System.Collections.Generic;
using System.Linq;

namespace ImageMagitek.Codec;

/// <summary>
/// FlowGraphicsFormat describes properties relating to decoding/encoding a
/// general graphics format that is resizable
/// </summary>
public sealed class FlowGraphicsFormat : IGraphicsFormat
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
    /// The color depth of each pixel in bits per pixel
    /// </summary>
    public int ColorDepth { get; }

    /// <summary>
    /// ColorType defines how pixel data is translated into color data
    /// </summary>
    public PixelColorType ColorType { get; set; }

    /// <summary>
    /// Specifies how individual bits of each color are merged according to priority
    ///   Ex: [3, 2, 0, 1] implies the first bit read will merge into plane 3,
    ///   second bit read into plane 2, third bit read into plane 0, fourth bit read into plane 1
    /// </summary>
    public int[] MergePlanePriority { get; }

    /// <summary>
    /// Default width of an element
    /// </summary>
    public int DefaultWidth { get; }

    /// <summary>
    /// Default height of an element
    /// </summary>
    public int DefaultHeight { get; }

    /// <summary>
    /// Current width of the elements to encode/decode
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Current height of elements to encode/decode
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Storage size of an element in bits
    /// </summary>
    /// <returns></returns>
    public int StorageSize => Width * Height * ColorDepth;

    public IList<ImageProperty> ImageProperties { get; set; } = new List<ImageProperty>();

    public FlowGraphicsFormat(string name, PixelColorType colorType, int colorDepth,
        ImageLayout layout, int defaultHeight, int defaultWidth, int[] mergePlanePriority)
    {
        Name = name;
        ColorType = colorType;
        ColorDepth = colorDepth;
        Layout = layout;
        DefaultHeight = defaultHeight;
        DefaultWidth = defaultWidth;

        Height = defaultHeight;
        Width = defaultWidth;
        MergePlanePriority = mergePlanePriority;
    }

    public void Resize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public IGraphicsFormat Clone()
    {
        var clone = new FlowGraphicsFormat(Name, ColorType, ColorDepth, Layout, DefaultWidth, DefaultHeight, MergePlanePriority.ToArray())
        {
            FixedSize = FixedSize,
            ImageProperties = ImageProperties.Select(x => new ImageProperty(x.ColorDepth, x.RowInterlace, x.RowPixelPattern)).ToList(),
        };

        return clone;
    }
}
