using System.Collections.Generic;
using System.Drawing;
using ImageMagitek.Colors;

namespace ImageMagitek.ExtensionMethods;

public static class DirectImageExtensions
{
    /// <summary>
    /// Fills the surrounding, contiguous color area with a new color
    /// </summary>
    /// <param name="x">x-coordinate to start at in pixel coordinates</param>
    /// <param name="y">y-coordinate to start at in pixel coordinates</param>
    /// <param name="fillColor">Color to fill with</param>
    /// <returns>True if any pixels were modified</returns>
    public static bool FloodFill(this DirectImage image, int x, int y, ColorRgba32 fillColor) =>
        FloodFill(image, x, y, fillColor, null);

    /// <summary>
    /// Fills the surrounding, contiguous color area with a new color, constrained by optional clip bounds
    /// </summary>
    /// <param name="x">x-coordinate to start at in pixel coordinates</param>
    /// <param name="y">y-coordinate to start at in pixel coordinates</param>
    /// <param name="fillColor">Color to fill with</param>
    /// <param name="clipBounds">Optional clip rectangle to constrain the fill area</param>
    /// <returns>True if any pixels were modified</returns>
    public static bool FloodFill(this DirectImage image, int x, int y, ColorRgba32 fillColor, Rectangle? clipBounds)
    {
        int minX = clipBounds?.Left ?? 0;
        int minY = clipBounds?.Top ?? 0;
        int maxX = clipBounds?.Right ?? image.Width;
        int maxY = clipBounds?.Bottom ?? image.Height;

        bool isModified = false;
        var replaceColor = image.GetPixel(x, y);

        if (fillColor.Color == replaceColor.Color)
            return false;

        var openNodes = new Stack<(int x, int y)>();
        openNodes.Push((x, y));

        while (openNodes.Count > 0)
        {
            var nodePosition = openNodes.Pop();

            if (nodePosition.x >= minX && nodePosition.x < maxX && nodePosition.y >= minY && nodePosition.y < maxY)
            {
                var nodeColor = image.GetPixel(nodePosition.x, nodePosition.y);
                if (nodeColor.Color == replaceColor.Color)
                {
                    isModified = true;
                    image.SetPixel(nodePosition.x, nodePosition.y, fillColor);
                    openNodes.Push((nodePosition.x - 1, nodePosition.y));
                    openNodes.Push((nodePosition.x + 1, nodePosition.y));
                    openNodes.Push((nodePosition.x, nodePosition.y - 1));
                    openNodes.Push((nodePosition.x, nodePosition.y + 1));
                }
            }
        }

        return isModified;
    }
}
