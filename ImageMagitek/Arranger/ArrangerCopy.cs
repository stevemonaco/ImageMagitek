using System;
using System.Drawing;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using ImageMagitek.Project;

namespace ImageMagitek;

public abstract class ArrangerCopy
{
    /// <summary>
    /// Color type of the source arranger
    /// </summary>
    public PixelColorType ColorType { get; protected set; }

    /// <summary>
    /// Element pixel size of the source arranger
    /// </summary>
    public Size ElementPixelSize { get; protected set; }

    /// <summary>
    /// Starting x-coordinate of copy in copy units
    /// </summary>
    public int X { get; protected set; }

    /// <summary>
    /// Starting y-coordinate of copy in copy units
    /// </summary>
    public int Y { get; protected set; }

    /// <summary>
    /// Width of copy in copy units
    /// </summary>
    public int Width { get; protected set; }

    /// <summary>
    /// Width of copy in copy units
    /// </summary>
    public int Height { get; protected set; }
}

public sealed class ElementCopy : ArrangerCopy
{
    /// <summary>
    /// Elements to be copied into the destination
    /// </summary>
    public ArrangerElement?[,] Elements { get; }

    /// <summary>
    /// Originating project resource
    /// </summary>
    public IProjectResource ProjectResource { get; set; }

    /// <summary>
    /// Layout of the source arranger
    /// </summary>
    public ElementLayout Layout { get; }

    /// <summary>
    /// Width of each element in pixels
    /// </summary>
    public int ElementPixelWidth { get; }

    /// <summary>
    /// Height of each element in pixels
    /// </summary>
    public int ElementPixelHeight { get; }

    /// <summary>
    /// Creates a copy of an Arranger's ArrangerElements
    /// </summary>
    /// <param name="source">Arranger containing the elements to be copied from</param>
    /// <param name="elementX">Starting x-coordinate of copy in element coordinates</param>
    /// <param name="elementY">Starting y-coordinate of copy in element coordinates</param>
    /// <param name="copyWidth">Width of copy in element coordinates</param>
    /// <param name="copyHeight">Height of copy in element coordinates</param>
    public ElementCopy(Arranger source, int elementX, int elementY, int copyWidth, int copyHeight)
    {
        ColorType = source.ColorType;
        Layout = source.Layout;
        ProjectResource = source;
        X = elementX;
        Y = elementY;
        Width = copyWidth;
        Height = copyHeight;
        ElementPixelWidth = source.ElementPixelSize.Width;
        ElementPixelHeight = source.ElementPixelSize.Height;
        ElementPixelSize = source.ElementPixelSize;

        Elements = new ArrangerElement?[copyWidth, copyHeight];

        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
                Elements[x, y] = source.GetElement(x + elementX, y + elementY);
        }
    }
}

public sealed class IndexedPixelCopy : ArrangerCopy
{
    public IndexedImage Image { get; }

    public IndexedPixelCopy(Arranger source, int pixelX, int pixelY, int width, int height)
    {
        ColorType = source.ColorType;
        ElementPixelSize = source.ElementPixelSize;
        X = pixelX;
        Y = pixelY;
        Width = width;
        Height = height;

        Image = new IndexedImage(source, X, Y, Width, Height);
    }
}

public sealed class DirectPixelCopy : ArrangerCopy
{
    public DirectImage Image { get; }

    public DirectPixelCopy(Arranger source, int pixelX, int pixelY, int width, int height)
    {
        ColorType = source.ColorType;
        ElementPixelSize = source.ElementPixelSize;
        X = pixelX;
        Y = pixelY;
        Width = width;
        Height = height;

        Image = new DirectImage(source, X, Y, Width, Height);
    }
}

public static class ElementCopyExtensions
{
    public static ArrangerCopy ToPixelCopy(this ElementCopy copy)
    {
        // Build a temporary arranger from the copy's elements to render pixel data
        var tempArranger = new ScatteredArranger(
            "copy", copy.ColorType, copy.Layout,
            copy.Width, copy.Height,
            copy.ElementPixelWidth, copy.ElementPixelHeight);

        for (int y = 0; y < copy.Height; y++)
        {
            for (int x = 0; x < copy.Width; x++)
            {
                var el = copy.Elements[x, y];
                tempArranger.SetElement(el, x, y);
            }
        }

        int pixelWidth = copy.Width * copy.ElementPixelWidth;
        int pixelHeight = copy.Height * copy.ElementPixelHeight;

        if (copy.ColorType == PixelColorType.Indexed)
        {
            return new IndexedPixelCopy(tempArranger, 0, 0, pixelWidth, pixelHeight);
        }
        else if (copy.ColorType == PixelColorType.Direct)
        {
            return new DirectPixelCopy(tempArranger, 0, 0, pixelWidth, pixelHeight);
        }
        else
        {
            throw new InvalidOperationException($"{nameof(ToPixelCopy)}: ElementCopy has an invalid color type {copy.ColorType}");
        }
    }
}
