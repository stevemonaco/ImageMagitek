using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek
{
    /// <summary>
    /// Provides functionality to work with pixel data of Arrangers with palettized graphics
    /// </summary>
    public class IndexedImage : ImageBase<byte>
    {
        /// <summary>
        /// Creates an IndexedImage of an Arranger
        /// </summary>
        /// <param name="arranger">Source Arranger</param>
        public IndexedImage(Arranger arranger) : 
            this(arranger, 0, 0, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height)
        {
        }

        /// <summary>
        /// Created an IndexedImage with a subsection of an Arranger
        /// </summary>
        /// <param name="arranger">Source Arranger</param>
        /// <param name="x">Left-edge of subsection in pixel coordinates</param>
        /// <param name="y">Top-edge of subsection in pixel coordinates</param>
        /// <param name="width">Width of subsection in pixel coordinates</param>
        /// <param name="height">Height of subsection in pixel coordinates</param>
        public IndexedImage(Arranger arranger, int x, int y, int width, int height)
        {
            if (arranger is null)
                throw new ArgumentNullException($"{nameof(IndexedImage)}.Ctor parameter '{nameof(arranger)}' was null");

            if (arranger.ColorType != PixelColorType.Indexed)
                throw new ArgumentException($"{nameof(IndexedImage)}.Ctor: Arranger '{arranger.Name}' has an invalid color type '{arranger.ColorType}'");

            Arranger = arranger;
            Left = x;
            Top = y;
            Width = width;
            Height = height;

            Image = new byte[Width * Height];
            Render();
        }

        public override void ExportImage(string imagePath, IImageFileAdapter adapter) =>
            adapter.SaveImage(Image, Arranger, imagePath);

        public void ImportImage(string imagePath, IImageFileAdapter adapter, ColorMatchStrategy matchStrategy)
        {
            var importImage = adapter.LoadImage(imagePath, Arranger, matchStrategy);
            importImage.CopyTo(Image, 0);
        }

        public MagitekResult TryImportImage(string imagePath, IImageFileAdapter adapter, ColorMatchStrategy matchStrategy)
        {
            var result = adapter.TryLoadImage(imagePath, Arranger, matchStrategy, out var importImage);

            if (result.Value is MagitekResult.Success)
                importImage.CopyTo(Image, 0);

            return result;
        }

        public override void Render()
        {
            if (Width <= 0 || Height <= 0)
                throw new InvalidOperationException($"{nameof(Render)}: arranger dimensions for '{Arranger.Name}' are too small to render " +
                    $"({Width}, {Height})");

            if (Width * Height != Image.Length)
                Image = new byte[Width * Height];

            var elements = Arranger.EnumerateElementsByPixel(Left, Top, Width, Height)
                .Where(x => x.DataFile is object);

            Rectangle imageRect = new Rectangle(Left, Top, Width, Height);

            foreach (var el in elements)
            {
                if (el.Codec is IIndexedCodec codec)
                {
                    var encodedBuffer = codec.ReadElement(el);

                    // TODO: Detect reads past end of file more gracefully
                    if (encodedBuffer.Length == 0)
                        continue;

                    var decodedImage = codec.DecodeElement(el, encodedBuffer);

                    var elementRect = new Rectangle(el.X1, el.Y1, el.Width, el.Height);
                    elementRect.Intersect(imageRect);

                    if (elementRect.IsEmpty)
                        continue;

                    int minX = Math.Clamp(el.X1, Left, Right - 1);
                    int maxX = Math.Clamp(el.X2, Left, Right - 1);
                    int minY = Math.Clamp(el.Y1, Top, Bottom - 1);
                    int maxY = Math.Clamp(el.Y2, Top, Bottom - 1);
                    int deltaX = minX - el.X1;
                    int deltaY = minY - el.Y1;

                    for (int y = 0; y <= maxY - minY; y++)
                    {
                        int destidx = (el.Y1 + deltaY + y - Top) * Width + (el.X1 + deltaX - Left);
                        for (int x = 0; x <= maxX - minX; x++)
                        {
                            Image[destidx] = decodedImage[x + deltaX, y + deltaY];
                            destidx++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Saves the IndexedImage to the underlying DataFile
        /// </summary>
        public override void SaveImage()
        {
            var buffer = new byte[Arranger.ElementPixelSize.Width, Arranger.ElementPixelSize.Height];
            
            foreach (var el in Arranger.EnumerateElements().Where(x => x.Codec is IIndexedCodec))
            {
                Image.CopyToArray(buffer, el.X1, el.Y1, Width, el.Width, el.Height);
                var codec = el.Codec as IIndexedCodec;
                var encodedImage = codec.EncodeElement(el, buffer);
                codec.WriteElement(el, encodedImage);
            }

            foreach (var fs in Arranger.EnumerateElements().Select(x => x.DataFile?.Stream).OfType<Stream>().Distinct())
                fs.Flush();
        }

        /// <summary>
        /// Sets a pixel's palette index at the specified pixel coordinate
        /// </summary>
        /// <param name="x">x-coordinate in subregion pixel coordinates</param>
        /// <param name="y">y-coordinate in subregion pixel coordinates</param>
        /// <param name="color">Palette index of color</param>
        public override void SetPixel(int x, int y, byte color)
        {
            if (x >= Width || y >= Height || x < 0 || y < 0)
                throw new ArgumentOutOfRangeException($"{nameof(SetPixel)} ({nameof(x)}: {x}, {nameof(y)}: {y}) were outside the image bounds ({nameof(Width)}: {Width}, {nameof(Height)}: {Height}");

            var el = Arranger.GetElementAtPixel(x, y);
            var codecColors = 1 << el.Codec.ColorDepth;
            var pal = Arranger.GetElementAtPixel(x, y).Palette;

            if (color >= pal.Entries)
                throw new ArgumentOutOfRangeException($"{nameof(SetPixel)} ({nameof(color)} ({color}): exceeded the number of entries in palette '{pal.Name}' ({pal.Entries})");
            if (color >= codecColors)
                throw new ArgumentOutOfRangeException($"{nameof(SetPixel)} ({nameof(color)} ({color}): index exceeded the max number of colors in codec '{el.Codec.Name}' ({codecColors})");

            Image[x + Width * y] = color;
        }

        /// <summary>
        /// Tries to set a pixel's palette index at the specified pixel coordinate
        /// </summary>
        /// <param name="x">x-coordinate in pixel coordinates</param>
        /// <param name="y">y-coordinate in pixel coordinates</param>
        /// <param name="color">Palette index of color</param>
        /// <returns>True if set, false if not set</returns>
        public MagitekResult TrySetPixel(int x, int y, ColorRgba32 color)
        {
            var result = CanSetPixel(x, y, color);

            if (result.Value is MagitekResult.Success)
                SetPixel(x, y, color);

            return result;
        }

        /// <summary>
        /// Sets a pixel's palette index at the specified pixel coordinate
        /// </summary>
        /// <param name="x">x-coordinate in pixel coordinates</param>
        /// <param name="y">y-coordinate in pixel coordinates</param>
        /// <param name="color">Palette index of color</param>
        public void SetPixel(int x, int y, ColorRgba32 color)
        {
            var elem = Arranger.GetElementAtPixel(x, y);
            var pal = elem.Palette;

            var index = pal.GetIndexByNativeColor(color, ColorMatchStrategy.Exact);
            Image[x + Width * y] = index;
        }

        /// <summary>
        /// Determines if a color can be set to an existing pixel's palette index at the specified pixel coordinate
        /// </summary>
        /// <param name="x">x-coordinate in pixel coordinates</param>
        /// <param name="y">y-coordinate in pixel coordinates</param>
        /// <param name="color">Palette index of color</param>
        public MagitekResult CanSetPixel(int x, int y, ColorRgba32 color)
        {
            if (x >= Width || y >= Height || x < 0 || y < 0)
                return new MagitekResult.Failed($"Cannot set pixel at ({x}, {y}) because because it is outside of the Arranger");

            var elem = Arranger.GetElementAtPixel(x, y);
            if (!(elem.Codec is IIndexedCodec))
                return new MagitekResult.Failed($"Cannot set pixel at ({x}, {y}) because the element's codec is not an indexed color type");

            var pal = elem.Palette;

            if (pal is null)
                return new MagitekResult.Failed($"Cannot set pixel at ({x}, {y}) because arranger '{Arranger.Name}' has no palette specified for the element");

            if (!pal.ContainsNativeColor(color))
                return new MagitekResult.Failed($"Cannot set pixel at ({x}, {y}) because the palette '{pal.Name}' does not contain the native color ({color.R}, {color.G}, {color.B}, {color.A})");

            return MagitekResult.SuccessResult;
        }

        /// <summary>
        /// Gets the pixel's native color at the specified pixel coordinate
        /// </summary>
        /// <param name="x">x-coordinate in pixel coordinates</param>
        /// <param name="y">y-coordinate in pixel coordinates</param>
        public ColorRgba32 GetPixelColor(int x, int y)
        {
            var pal = Arranger.GetElementAtPixel(x, y).Palette;
            var palIndex = Image[x + Width * y];
            return pal[palIndex];
        }

        /// <summary>
        /// Tries to set the palette to the ArrangerElement containing the specified pixel coordinate
        /// </summary>
        /// <param name="x">x-coordinate in pixel coordinates</param>
        /// <param name="y">y-coordinate in pixel coordinates</param>
        /// <param name="pal">Palette to be set, if possible</param>
        /// <returns></returns>
        public MagitekResult TrySetPalette(int x, int y, Palette pal)
        {
            if (x >= Arranger.ArrangerPixelSize.Width || y >= Arranger.ArrangerPixelSize.Height)
                return new MagitekResult.Failed($"Cannot assign the palette because the location ({x}, {y}) is outside of the arranger " +
                    $"'{Arranger.Name}' bounds  ({Arranger.ArrangerPixelSize.Width}, {Arranger.ArrangerPixelSize.Height})");

            var el = Arranger.GetElementAtPixel(x, y);

            if (ReferenceEquals(pal, el.Palette))
                return MagitekResult.SuccessResult;

            int maxIndex = 0;

            for (int pixelY = el.Y1; pixelY <= el.Y2; pixelY++)
                for (int pixelX = el.X1; pixelX <= el.X2; pixelX++)
                    maxIndex = Math.Max(maxIndex, GetPixel(pixelX, pixelY));

            if (maxIndex < pal.Entries)
            {
                var location = Arranger.PointToElementLocation(new System.Drawing.Point(x, y));

                el = el.WithPalette(pal);

                Arranger.SetElement(el, location.X, location.Y);
                return MagitekResult.SuccessResult;
            }
            else
                return new MagitekResult.Failed($"Cannot assign the palette '{pal.Name}' because the element contains a palette index ({maxIndex}) outside of the palette");
        }

        /// <summary>
        /// Remaps the colors of the image to new colors
        /// </summary>
        /// <param name="remap">List containing remapped indices</param>
        public void RemapColors(IList<byte> remap) =>
            Image = Image.Select(x => remap[x]).ToArray();
    }
}