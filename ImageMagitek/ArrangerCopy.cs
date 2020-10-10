using ImageMagitek.Colors;
using System;

namespace ImageMagitek
{
    public abstract class ArrangerCopy
    {
        public Arranger Source { get; protected set; }
        public int X { get; protected set; }
        public int Y { get; protected set; }
        public int Width { get; protected set; }
        public int Height { get; protected set; }
    }

    public class ElementCopy : ArrangerCopy
    {
        public ArrangerElement[,] Elements { get; }

        /// <summary>
        /// Creates a copy of an Arranger's ArrangerElements
        /// </summary>
        /// <param name="source"></param>
        /// <param name="elementX">Starting x-coordinate of copy in element coordinates</param>
        /// <param name="elementY">Starting y-coordinate of copy in element coordinates</param>
        /// <param name="width">Width of copy in element coordinates</param>
        /// <param name="height">Height of copy in element coordinates</param>
        public ElementCopy(Arranger source, int elementX, int elementY, int width, int height)
        {
            Source = source;
            X = elementX;
            Y = elementY;
            Width = width;
            Height = height;
            Elements = new ArrangerElement[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    Elements[x, y] = source.GetElement(x + elementX, y + elementY);
            }
        }
    }

    public class IndexedPixelCopy : ArrangerCopy
    {
        public IndexedImage Image { get; }

        public IndexedPixelCopy(Arranger source, int pixelX, int pixelY, int width, int height)
        {
            Source = source;
            X = pixelX;
            Y = pixelY;
            Width = width;
            Height = height;

            Image = new IndexedImage(Source, X, Y, Width, Height);
        }
    }

    public class DirectPixelCopy : ArrangerCopy
    {
        public DirectImage Image { get; }

        public DirectPixelCopy(Arranger source, int pixelX, int pixelY, int width, int height)
        {
            Source = source;
            X = pixelX;
            Y = pixelY;
            Width = width;
            Height = height;

            Image = new DirectImage(Source, X, Y, Width, Height);
        }
    }

    public static class ElementCopyExtensions
    {
        public static ArrangerCopy ToPixelCopy(this ElementCopy copy)
        {
            int x = copy.X * copy.Source.ElementPixelSize.Width;
            int y = copy.Y * copy.Source.ElementPixelSize.Height;
            int width = copy.Width * copy.Source.ElementPixelSize.Width;
            int height = copy.Height * copy.Source.ElementPixelSize.Height;

            if (copy.Source.ColorType == PixelColorType.Indexed)
            {
                return copy.Source.CopyPixelsIndexed(x, y, width, height);
            }
            else if (copy.Source.ColorType == PixelColorType.Direct)
            {
                return copy.Source.CopyPixelsDirect(x, y, width, height);
            }
            else
            {
                throw new InvalidOperationException($"{nameof(ToPixelCopy)}: Arranger {copy.Source.Name} has an invalid color type {copy.Source.ColorType}");
            }
        }
    }
}
