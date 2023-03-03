using System;
using System.Collections.Generic;
using System.Drawing;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.ExtensionMethods;

public static class IndexedImageExtensions
{
    /// <summary>
    /// Sets a pixel's palette index at the specified pixel coordinate
    /// </summary>
    /// <param name="x">x-coordinate in pixel coordinates</param>
    /// <param name="y">y-coordinate in pixel coordinates</param>
    /// <param name="color">Palette index of color</param>
    public static void SetPixel(this IndexedImage image, int x, int y, ColorRgba32 color)
    {
        var el = image.Arranger.GetElementAtPixel(x + image.Left, y + image.Top);

        if (el?.Palette is Palette pal)
        {
            var index = pal.GetIndexByNativeColor(color, ColorMatchStrategy.Exact);
            image.Image[x + image.Width * y] = index;
        }
        else
            throw new InvalidOperationException($"{nameof(SetPixel)} cannot set pixel at ({x}, {y}) because there is no associated palette");
    }

    /// <summary>
    /// Tries to set a pixel's palette index at the specified pixel coordinate
    /// </summary>
    /// <param name="x">x-coordinate in pixel coordinates</param>
    /// <param name="y">y-coordinate in pixel coordinates</param>
    /// <param name="color">Palette index of color</param>
    /// <returns>True if set, false if not set</returns>
    public static MagitekResult TrySetPixel(this IndexedImage image, int x, int y, ColorRgba32 color)
    {
        var result = image.CanSetPixel(x, y, color);

        if (result.Value is MagitekResult.Success)
            image.SetPixel(x, y, color);

        return result;
    }

    /// <summary>
    /// Determines if a color can be set to an existing pixel's palette index at the specified pixel coordinate
    /// </summary>
    /// <param name="x">x-coordinate in pixel coordinates</param>
    /// <param name="y">y-coordinate in pixel coordinates</param>
    /// <param name="color">Palette index of color</param>
    public static MagitekResult CanSetPixel(this IndexedImage image, int x, int y, ColorRgba32 color)
    {
        if (x >= image.Width || y >= image.Height || x < 0 || y < 0)
            return new MagitekResult.Failed($"Cannot set pixel at ({x}, {y}) because because it is outside of the Arranger");

        var el = image.Arranger.GetElementAtPixel(x + image.Left, y + image.Top);

        if (el is ArrangerElement element)
        {
            if (!(element.Codec is IIndexedCodec))
                return new MagitekResult.Failed($"Cannot set pixel at ({x}, {y}) because the element's codec is not an indexed color type");

            var pal = element.Palette;

            if (pal is null)
                return new MagitekResult.Failed($"Cannot set pixel at ({x}, {y}) because arranger '{image.Arranger.Name}' has no palette specified for the element");

            if (!pal.ContainsNativeColor(color))
                return new MagitekResult.Failed($"Cannot set pixel at ({x}, {y}) because the palette '{pal.Name}' does not contain the native color ({color.R}, {color.G}, {color.B}, {color.A})");

            var index = pal.GetIndexByNativeColor(color, ColorMatchStrategy.Exact);
            if (index >= (1 << element.Codec.ColorDepth))
                return new MagitekResult.Failed($"Cannot set pixel at ({x}, {y}) because the color is contained at an index outside of the codec's range");

            return MagitekResult.SuccessResult;
        }
        else
            return new MagitekResult.Failed($"Cannot set pixel at ({x}, {y}) because the element is undefined");
    }

    /// <summary>
    /// Gets the pixel's native color at the specified pixel coordinate
    /// </summary>
    /// <param name="x">x-coordinate in pixel coordinates</param>
    /// <param name="y">y-coordinate in pixel coordinates</param>
    public static ColorRgba32 GetPixelColor(this IndexedImage image, int x, int y)
    {
        var el = image.Arranger.GetElementAtPixel(x + image.Left, y + image.Top);

        if (el?.Palette is Palette pal)
        {
            var palIndex = image.Image[x + image.Width * y];
            return pal[palIndex];
        }
        else
            throw new InvalidOperationException($"{nameof(GetPixelColor)} has no defined palette at ({x}, {y})");
    }

    /// <summary>
    /// Tries to set the palette to the ArrangerElement containing the specified pixel coordinate
    /// </summary>
    /// <param name="x">x-coordinate in pixel coordinates</param>
    /// <param name="y">y-coordinate in pixel coordinates</param>
    /// <param name="pal">Palette to be set, if possible</param>
    /// <returns></returns>
    public static MagitekResult TrySetPalette(this IndexedImage image, int x, int y, Palette pal)
    {
        if (x + image.Left >= image.Arranger.ArrangerPixelSize.Width || y + image.Top >= image.Arranger.ArrangerPixelSize.Height)
            return new MagitekResult.Failed($"Cannot assign the palette because the location ({x}, {y}) is outside of the arranger " +
                $"'{image.Arranger.Name}' bounds  ({image.Arranger.ArrangerPixelSize.Width}, {image.Arranger.ArrangerPixelSize.Height})");

        var el = image.Arranger.GetElementAtPixel(x + image.Left, y + image.Top);

        if (el is ArrangerElement element)
        {
            if (ReferenceEquals(pal, element.Palette))
                return MagitekResult.SuccessResult;

            int maxIndex = 0;

            for (int pixelY = element.Y1; pixelY <= element.Y2; pixelY++)
                for (int pixelX = element.X1; pixelX <= element.X2; pixelX++)
                    maxIndex = Math.Max(maxIndex, image.GetPixel(pixelX, pixelY));

            if (maxIndex < pal.Entries)
            {
                var location = image.Arranger.PointToElementLocation(new Point(x + image.Left, y + image.Top));

                element = element.WithPalette(pal);

                image.Arranger.SetElement(element, location.X, location.Y);
                return MagitekResult.SuccessResult;
            }
            else
                return new MagitekResult.Failed($"Cannot assign the palette '{pal.Name}' because the element contains a palette index ({maxIndex}) outside of the palette");
        }
        else
            return new MagitekResult.Failed($"Cannot assign the palette '{pal.Name}' because the element is undefined");
    }

    /// <summary>
    /// Fills the surrounding, contiguous color area with a new color
    /// </summary>
    /// <param name="x">x-coordinate to start at in pixel coordinates</param>
    /// <param name="y">y-coordinate to start at in pixel coordinates</param>
    /// <param name="fillIndex">Palette index to fill with</param>
    /// <returns>True if any pixels were modified</returns>
    public static bool FloodFill(this IndexedImage image, int x, int y, byte fillIndex)
    {
        bool isModified = false;
        var replaceIndex = image.GetPixel(x, y);
        var startingPalette = image.GetElementAtPixel(x, y)?.Palette;

        if (fillIndex == replaceIndex || startingPalette is null)
            return false;

        var openNodes = new Stack<(int x, int y)>();
        openNodes.Push((x, y));

        while (openNodes.Count > 0)
        {
            var nodePosition = openNodes.Pop();

            if (nodePosition.x >= 0 && nodePosition.x < image.Width && nodePosition.y >= 0 && nodePosition.y < image.Height)
            {
                var nodeColor = image.GetPixel(nodePosition.x, nodePosition.y);
                if (nodeColor == replaceIndex)
                {
                    var destPalette = image.GetElementAtPixel(nodePosition.x, nodePosition.y)?.Palette;
                    if (ReferenceEquals(startingPalette, destPalette))
                    {
                        isModified = true;
                        image.SetPixel(nodePosition.x, nodePosition.y, fillIndex);
                        openNodes.Push((nodePosition.x - 1, nodePosition.y));
                        openNodes.Push((nodePosition.x + 1, nodePosition.y));
                        openNodes.Push((nodePosition.x, nodePosition.y - 1));
                        openNodes.Push((nodePosition.x, nodePosition.y + 1));
                    }
                }
            }
        }

        return isModified;
    }
}
