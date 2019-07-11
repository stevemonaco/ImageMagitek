using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek
{
    /// <summary>
    /// Provides support for rendering Arrangers to an image, manipulating pixel data, and saving to disk with sequential operations
    /// </summary>
    public class ArrangerImage : IArrangerImage<Rgba32>, IDisposable
    {
        public Image<Rgba32> Image { get; set; }
        bool NeedsRedraw = true;
        bool Disposed = false;

        /// <summary>
        /// Renders an image using the specified arranger
        /// Invalidate must be called to force a new render
        /// </summary>
        /// <param name="arranger"></param>
        /// <returns></returns>
        public bool Render(Arranger arranger)
        {
            if (arranger is null)
                throw new ArgumentNullException($"{nameof(Render)} parameter '{nameof(arranger)}' was null");
            if (arranger.ArrangerPixelSize.Width <= 0 || arranger.ArrangerPixelSize.Height <= 0)
                throw new InvalidOperationException($"{nameof(Render)}: arranger dimensions too small to render " + 
                    $"({arranger.ArrangerPixelSize.Width}, {arranger.ArrangerPixelSize.Height})");

            if (Image is null || arranger.ArrangerPixelSize.Height != Image.Height || arranger.ArrangerPixelSize.Width != Image.Width)
                Image = new Image<Rgba32>(arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height);

            if (!NeedsRedraw)
                return true;

            // TODO: Consider using Tile Cache

            foreach(var el in arranger.EnumerateElements())
                el.Codec.Decode(Image, el);

            NeedsRedraw = false;

            return true;
        }

        public bool LoadImage(string imageFileName)
        {
            Image = SixLabors.ImageSharp.Image.Load(imageFileName);
            return true;
        }

        /// <summary>
        /// Saves the currently edited image to the underlying source using the specified arranger for placement and encoding
        /// </summary>
        /// <param name="arranger"></param>
        /// <returns></returns>
        public bool SaveImage(Arranger arranger)
        {
            if (arranger is null)
                throw new ArgumentNullException($"{nameof(SaveImage)} parameter '{nameof(arranger)}' was null");

            if (arranger.ArrangerPixelSize.Width <= 0 || arranger.ArrangerPixelSize.Height <= 0)
                throw new ArgumentException($"{nameof(SaveImage)} parameter '{nameof(arranger)}' has invalid dimensions" + 
                    $"({arranger.ArrangerPixelSize.Width}, {arranger.ArrangerPixelSize.Height})");

            if (arranger.Mode == ArrangerMode.SequentialArranger)
                throw new InvalidOperationException($"{nameof(SaveImage)} parameter '{nameof(arranger)}' is in invalid {nameof(ArrangerMode)} ({arranger.Mode.ToString()})");

            if (Image is null)
                throw new NullReferenceException($"{nameof(SaveImage)} property '{nameof(Image)}' was null");

            if (arranger.ArrangerPixelSize.Width != Image.Width || arranger.ArrangerPixelSize.Height != Image.Height)
                throw new InvalidOperationException($"{nameof(SaveImage)} has mismatched dimensions: " + 
                    $"'{nameof(arranger)}' ({arranger.ArrangerPixelSize.Width}, {arranger.ArrangerPixelSize.Height}) '{nameof(Image)} ({Image.Width}, {Image.Height})'");

            foreach(var el in arranger.EnumerateElements())
                el.Codec.Encode(Image, el);

            return true;
        }

        /// <summary>
        /// Forces a redraw for next Render call
        /// </summary>
        public void Invalidate()
        {
            NeedsRedraw = true;
        }

        /// <summary>
        /// Gets the local color of a pixel at the specified coordinate
        /// </summary>
        /// <param name="x">x-coordinate</param>
        /// <param name="y">y-coordinate</param>
        /// <returns>Local color</returns>
        public Rgba32 GetPixel(int x, int y)
        {
            if (Image is null)
                throw new NullReferenceException($"{nameof(GetPixel)} property '{nameof(Image)}' was null");

            return Image[x, y];
        }

        /// <summary>
        /// Sets the pixel of the image to a specified color
        /// </summary>
        /// <param name="x">x-coordinate of pixel</param>
        /// <param name="y">y-coordinate of pixel</param>
        /// <param name="color">Local color to set</param>
        public void SetPixel(int x, int y, Rgba32 color)
        {
            if (Image is null)
                throw new NullReferenceException($"{nameof(SetPixel)} property '{nameof(Image)}' was null");

            Image[x, y] = color;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
                return;

            if(disposing)
            {
                Image.Dispose();
            }

            Disposed = true;
        }

        ~ArrangerImage()
        {
            Dispose(false);
        }
    }
}
