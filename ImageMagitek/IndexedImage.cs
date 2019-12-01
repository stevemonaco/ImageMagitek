using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek
{
    public class IndexedImage
    {
        public byte[,] Image { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public IndexedImage(int width, int height)
        {
            Width = width;
            Height = height;
            Image = new byte[Width, Height];
        }

        /// <summary>
        /// Gets the palette-indexed color at the specified coordinate
        /// </summary>
        /// <param name="x">x-coordinate</param>
        /// <param name="y">y-coordinate</param>
        /// <returns>Palette-indexed color</returns>
        public byte GetPixel(int x, int y)
        {
            if (Image is null)
                throw new NullReferenceException($"{nameof(GetPixel)} property '{nameof(Image)}' was null");

            return Image[x, y];
        }

        /// <summary>
        /// Sets the specified coordinate of the image to a specified palette-indexed color
        /// </summary>
        /// <param name="x">x-coordinate of pixel</param>
        /// <param name="y">y-coordinate of pixel</param>
        /// <param name="color">Palette-indexed color to set</param>
        public void SetPixel(int x, int y, byte color)
        {
            if (Image is null)
                throw new NullReferenceException($"{nameof(SetPixel)} property '{nameof(Image)}' was null");

            Image[x, y] = color;
        }
    }
}
