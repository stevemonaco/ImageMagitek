using System.Collections.Generic;
using ImageMagitek.Colors;

namespace ImageMagitek.ExtensionMethods;

public static class DirectImageExtensions
{
    /// <summary>
    /// Fills the surrounding, contiguous color area with a new color
    /// </summary>
    /// <param name="x">x-coordinate to start at in pixel coordinates</param>
    /// <param name="y">y-coordinate to start at in pixel coordinates</param>
    /// <param name="fillIndex">Palette index to fill with</param>
    /// <returns>True if any pixels were modified</returns>
    public static bool FloodFill(this DirectImage image, int x, int y, ColorRgba32 fillColor)
    {
        bool isModified = false;
        var replaceColor = image.GetPixel(x, y);

        if (fillColor.Color == replaceColor.Color)
            return false;

        var openNodes = new Stack<(int x, int y)>();
        openNodes.Push((x, y));

        while (openNodes.Count > 0)
        {
            var nodePosition = openNodes.Pop();

            if (nodePosition.x >= 0 && nodePosition.x < image.Width && nodePosition.y >= 0 && nodePosition.y < image.Height)
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
