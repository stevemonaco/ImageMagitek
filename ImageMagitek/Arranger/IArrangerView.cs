using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek;

/// <summary>
/// A read-only grid of elements that images render, export and write pixels through.
/// Every <see cref="Arranger"/> is one; a tilemap and a metatile map layer derive one from their entries.
/// </summary>
public interface IArrangerView
{
    string Name { get; }

    /// <summary>
    /// The size of the grid in elements
    /// </summary>
    Size ArrangerElementSize { get; }

    /// <summary>
    /// The size of each element in pixels
    /// </summary>
    Size ElementPixelSize { get; }

    /// <summary>
    /// The size of the grid in pixels
    /// </summary>
    Size ArrangerPixelSize { get; }

    ElementLayout Layout { get; }
    PixelColorType ColorType { get; }

    /// <summary>
    /// The element at (<paramref name="elemX"/>, <paramref name="elemY"/>) in element coordinates, or null for an empty cell
    /// </summary>
    ArrangerElement? GetElement(int elemX, int elemY);
}

/// <summary>
/// Enumeration and lookup over the elements of an <see cref="IArrangerView"/>
/// </summary>
public static class ArrangerViewExtensions
{
    /// <summary>
    /// Gets an Element from a position in the view in pixel coordinates
    /// </summary>
    public static ArrangerElement? GetElementAtPixel(this IArrangerView view, int pixelX, int pixelY)
    {
        var elX = pixelX / view.ElementPixelSize.Width;
        var elY = pixelY / view.ElementPixelSize.Height;

        if (elX >= view.ArrangerElementSize.Width || elY >= view.ArrangerElementSize.Height || pixelX < 0 || pixelY < 0)
            throw new ArgumentOutOfRangeException($"{nameof(GetElementAtPixel)} parameter was out of range: ({pixelX}, {pixelY})px, ({elX}, {elY})el");

        return view.GetElement(elX, elY);
    }

    /// <summary>
    /// Returns the enumeration of all Elements in the grid in a left-to-right, row-by-row order
    /// </summary>
    public static IEnumerable<ArrangerElement?> EnumerateElements(this IArrangerView view) =>
        view.EnumerateElements(0, 0, view.ArrangerElementSize.Width, view.ArrangerElementSize.Height);

    /// <summary>
    /// Returns the full enumeration of a subsection of all Elements in the grid in a left-to-right, row-by-row order
    /// </summary>
    /// <param name="elemX">Starting x-coordinate in element coordinates</param>
    /// <param name="elemY">Starting y-coordinate in element coordinates</param>
    /// <param name="width">Number of elements to enumerate in x-direction</param>
    /// <param name="height">Number of elements to enumerate in y-direction</param>
    public static IEnumerable<ArrangerElement?> EnumerateElements(this IArrangerView view, int elemX, int elemY, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                yield return view.GetElement(x + elemX, y + elemY);
            }
        }
    }

    /// <summary>
    /// Returns the enumeration of all element locations in the grid in a left-to-right, row-by-row order
    /// </summary>
    public static IEnumerable<(int x, int y)> EnumerateElementsWithinElementRange(this IArrangerView view)
    {
        var size = view.ArrangerElementSize;
        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                yield return (x, y);
            }
        }
    }

    /// <summary>
    /// Returns the full enumeration of a subsection of Elements in the grid in a left-to-right, row-by-row order
    /// </summary>
    /// <param name="pixelX">Starting x-coordinate in pixel coordinates</param>
    /// <param name="pixelY">Starting y-coordinate in pixel coordinates</param>
    /// <param name="width">Width of range in pixels</param>
    /// <param name="height">Height of range in pixels</param>
    public static IEnumerable<ArrangerElement?> EnumerateElementsWithinPixelRange(this IArrangerView view, int pixelX, int pixelY, int width, int height) =>
        view.EnumerateElementLocationsWithinPixelRange(pixelX, pixelY, width, height).Select(x => view.GetElement(x.X, x.Y));

    /// <summary>
    /// Returns the full enumeration of a subsection of Element locations in the grid in a left-to-right, row-by-row order
    /// </summary>
    /// <param name="pixelX">Starting x-coordinate in pixel coordinates</param>
    /// <param name="pixelY">Starting y-coordinate in pixel coordinates</param>
    /// <param name="width">Width of range in pixels</param>
    /// <param name="height">Height of range in pixels</param>
    /// <returns>A tuple with the x and y location of the element</returns>
    public static IEnumerable<(int X, int Y)> EnumerateElementLocationsWithinPixelRange(this IArrangerView view, int pixelX, int pixelY, int width, int height)
    {
        if (width <= 0 || height <= 0)
            yield break;

        var elementSize = view.ElementPixelSize;
        int elemX = pixelX / elementSize.Width;
        int elemY = pixelY / elementSize.Height;
        int elemX2 = (pixelX + width + elementSize.Width - 1) / elementSize.Width;
        int elemY2 = (pixelY + height + elementSize.Height - 1) / elementSize.Height;

        for (int y = elemY; y < elemY2; y++)
        {
            for (int x = elemX; x < elemX2; x++)
            {
                yield return (x, y);
            }
        }
    }

    /// <summary>
    /// Returns the set of distinct Palettes contained by the view's Elements
    /// </summary>
    public static HashSet<Palette> GetReferencedPalettes(this IArrangerView view) =>
        view.EnumerateElements()
            .OfType<ArrangerElement>()
            .Select(x => x.Codec)
            .OfType<IIndexedCodec>()
            .Select(x => x.Palette)
            .OfType<Palette>()
            .ToHashSet();
}
