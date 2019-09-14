using System;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace ImageMagitek
{
    public interface IArrangerImage<TPixel>
    {
        void Render();
        void RenderSubImage(int x, int y, int width, int height);
        bool LoadImage(string imageFileName);
        bool SaveImage(Arranger arranger);
        TPixel GetPixel(int x, int y);
        void SetPixel(int x, int y, TPixel color);
    }

    /// <summary>
    /// Provides support for rendering Arrangers to an image, manipulating pixel data, and saving to disk with sequential operations
    /// </summary>
    public class ArrangerImage : IArrangerImage<Rgba32>, IDisposable
    {
        public Image<Rgba32> Image { get; set; }

        private Arranger _arranger;
        private bool _needsRedraw = true;
        private bool _disposed = false;
        private BlankCodec _blankCodec = new BlankCodec();
        private Rectangle _renderRect = new Rectangle(0, 0, 0, 0);

        public Palette DefaultPalette { get; set; }

        public ArrangerImage(Arranger arranger)
        {
            _arranger = arranger;
        }

        /// <summary>
        /// Renders an image using the arranger
        /// Invalidate must be called to force a new render
        /// </summary>
        public void Render()
        {
            if (_arranger is null)
                throw new ArgumentNullException($"{nameof(Render)} parameter '{nameof(_arranger)}' was null");
            if (_arranger.ArrangerPixelSize.Width <= 0 || _arranger.ArrangerPixelSize.Height <= 0)
                throw new InvalidOperationException($"{nameof(Render)}: arranger dimensions too small to render " + 
                    $"({_arranger.ArrangerPixelSize.Width}, {_arranger.ArrangerPixelSize.Height})");

            if (Image is null || _arranger.ArrangerPixelSize.Height != Image.Height || _arranger.ArrangerPixelSize.Width != Image.Width)
                Image = new Image<Rgba32>(_arranger.ArrangerPixelSize.Width, _arranger.ArrangerPixelSize.Height);

            if (!_needsRedraw)
                return;

            // TODO: Consider using Tile Cache
            _renderRect = new Rectangle(0, 0, _arranger.ArrangerPixelSize.Width, _arranger.ArrangerPixelSize.Height);

            foreach (var el in _arranger.EnumerateElements())
            {
                if (el.Codec is null)
                    _blankCodec.Decode(Image, el);
                else
                    el.Codec.Decode(Image, el);
            }

            _needsRedraw = false;
        }

        /// <summary>
        /// Renders a rectangular section using the specified arranger
        /// Invalidate must be called to force a new render
        /// </summary>
        /// <param name="arranger"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public void RenderSubImage(int x, int y, int width, int height)
        {
            if (x < 0 || y < 0 || x >= _arranger.ArrangerPixelSize.Width || y >= _arranger.ArrangerPixelSize.Height)
                throw new ArgumentOutOfRangeException($"{nameof(RenderSubImage)} parameters {nameof(x)} '{x}' and {nameof(y)} '{y}' are outside of the arranger bounds");

            if(width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException($"{nameof(RenderSubImage)} parameters {nameof(width)} '{width}' and {nameof(height)} '{height}' must be greater than zero");

            if(x+width > _arranger.ArrangerPixelSize.Width || y+height > _arranger.ArrangerPixelSize.Height)
                throw new ArgumentException($"{nameof(RenderSubImage)} parameters ({x+width}, {y+height}) are outside of the arranger bounds ({_arranger.ArrangerPixelSize.Width}, {_arranger.ArrangerPixelSize.Height})");

            // Full render and crop required for linear layout selections
            Render();
            _renderRect = new Rectangle(x, y, width, height);
            Image.Mutate(x => x.Crop(_renderRect));
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

            foreach(var el in arranger.EnumerateElements().Where(x => x.Codec != null))
                el.Codec.Encode(Image, el);

            return true;
        }

        /// <summary>
        /// Forces a redraw for next Render call
        /// </summary>
        public void Invalidate()
        {
            _needsRedraw = true;
        }

        /// <summary>
        /// Gets the local color of a pixel at the specified coordinate
        /// </summary>
        /// <param name="x">x-coordinate</param>
        /// <param name="y">y-coordinate</param>
        /// <returns>Native color</returns>
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
        /// <param name="color">Native color to set</param>
        public void SetPixel(int x, int y, Rgba32 color)
        {
            if (Image is null)
                throw new NullReferenceException($"{nameof(SetPixel)} property '{nameof(Image)}' was null");

            Image[x, y] = color;
        }

        public bool TrySetPixel(int x, int y, Rgba32 color)
        {
            if (Image is null)
                throw new NullReferenceException($"{nameof(TrySetPixel)} property '{nameof(Image)}' was null");

            var elem = GetElement(x, y);

            if (elem.Codec is BlankCodec)
                return false;

            if (elem.Codec.ColorType == PixelColorType.Indexed)
                return TrySetIndexedPixel(elem, x, y, color);
            
            SetPixel(x, y, color);
            return true;
        }

        private bool TrySetIndexedPixel(ArrangerElement element, int x, int y, Rgba32 color)
        {
            var nc = new ColorRgba32(color.R, color.G, color.B, color.A);

            var pal = element.Palette ?? DefaultPalette;

            if (pal.ContainsNativeColor(nc))
            {
                var index = pal.GetIndexByNativeColor(nc, true);
                if (index >= (1 << element.Codec.ColorDepth))
                    return false;
            }

            SetPixel(x, y, color);
            return true;
        }

        private ArrangerElement GetElement(int x, int y)
        {
            var elemX = (_renderRect.X + x) / _arranger.ElementPixelSize.Width;
            var elemY = (_renderRect.Y + y) / _arranger.ElementPixelSize.Height;
            return _arranger.GetElement(elemX, elemY);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if(disposing)
            {
                Image.Dispose();
            }

            _disposed = true;
        }

        ~ArrangerImage()
        {
            Dispose(false);
        }
    }
}
