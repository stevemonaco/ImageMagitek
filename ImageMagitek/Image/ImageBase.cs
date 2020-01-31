using ImageMagitek.Codec;
using System;
using System.Collections.Generic;

namespace ImageMagitek
{
    /// <summary>
    /// Provides an editing/viewing layer around an Arranger
    /// </summary>
    /// <typeparam name="TPixel"></typeparam>
    public abstract class ImageBase<TPixel> where TPixel: struct
    {
        protected Arranger Arranger { get; set; }
        public TPixel[] Image { get; set; }
        public int Width => Arranger.ArrangerPixelSize.Width;
        public int Height => Arranger.ArrangerPixelSize.Height;

        public abstract void Render();
        public abstract void SaveImage();
        public abstract void ExportImage(string imagePath, IImageFileAdapter adapter);
        public abstract void ImportImage(string imagePath, IImageFileAdapter adapter);

        public virtual TPixel GetPixel(int x, int y)
        {
            if (Image is null)
                throw new NullReferenceException($"{nameof(GetPixel)} property '{nameof(Image)}' was null");

            if (x >= Width || y >= Height || x < 0 || y < 0)
                throw new ArgumentOutOfRangeException($"{nameof(GetPixel)} ({nameof(x)}: {x}, {nameof(y)}: {y}) were outside the image bounds ({nameof(Width)}: {Width}, {nameof(Height)}: {Height}");

            return Image[x + Width * y];
        }

        public virtual Span<TPixel> GetPixelSpan()
        {
            if (Image is null)
                throw new NullReferenceException($"{nameof(GetPixel)} property '{nameof(Image)}' was null");

            return new Span<TPixel>(Image);
        }

        public virtual Span<TPixel> GetRowPixelSpan(int y)
        {
            if (Image is null)
                throw new NullReferenceException($"{nameof(GetPixel)} property '{nameof(Image)}' was null");

            if (y >= Height || y < 0)
                throw new ArgumentOutOfRangeException($"{nameof(GetPixel)} {nameof(y)}: {y} was outside the image bounds ({nameof(Height)}: {Height}");

            return new Span<TPixel>(Image, Width * y, Width);
        }

        public virtual IEnumerable<TPixel> Pixels()
        {
            if (Image is null)
                throw new NullReferenceException($"{nameof(GetPixel)} property '{nameof(Image)}' was null");

            for (int i = 0; i < Width * Height; i++)
                yield return Image[i];
        }

        public virtual void SetPixel(int x, int y, TPixel color)
        {
            if (Image is null)
                throw new NullReferenceException($"{nameof(GetPixel)} property '{nameof(Image)}' was null");

            if (x >= Width || y >= Height || x < 0 || y < 0)
                throw new ArgumentOutOfRangeException($"{nameof(GetPixel)} ({nameof(x)}: {x}, {nameof(y)}: {y}) were outside the image bounds ({nameof(Width)}: {Width}, {nameof(Height)}: {Height}");

            Image[x + Width * y] = color;
        }
    }
}
