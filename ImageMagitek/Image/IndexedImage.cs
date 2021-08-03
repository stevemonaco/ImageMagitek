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
    public sealed class IndexedImage : ImageBase<byte>
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
        /// Creates an IndexedImage with a subsection of an Arranger
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

            // TODO: Handle undefined elements explicitly and clear image subsections
            Array.Clear(Image, 0, Image.Length);

            var locations = Arranger.EnumerateElementLocationsByPixel(Left, Top, Width, Height);

            Rectangle imageRect = new Rectangle(Left, Top, Width, Height);

            foreach (var location in locations)
            {
                var el = Arranger.GetElement(location.X, location.Y);
                if (el is ArrangerElement element && element.Codec is IIndexedCodec codec)
                {
                    var encodedBuffer = codec.ReadElement(element);

                    // TODO: Detect reads past end of file more gracefully
                    if (encodedBuffer.Length == 0)
                        continue;

                    var elementRect = new Rectangle(element.X1, element.Y1, element.Width, element.Height);
                    elementRect.Intersect(imageRect);

                    if (elementRect.IsEmpty)
                        continue;

                    int minX = Math.Clamp(element.X1, Left, Right - 1);
                    int maxX = Math.Clamp(element.X2, Left, Right - 1);
                    int minY = Math.Clamp(element.Y1, Top, Bottom - 1);
                    int maxY = Math.Clamp(element.Y2, Top, Bottom - 1);
                    int deltaX = minX - element.X1;
                    int deltaY = minY - element.Y1;

                    var decodedImage = codec.DecodeElement(element, encodedBuffer);
                    decodedImage.RotateArray2D(element.Rotation);
                    decodedImage.MirrorArray2D(element.Mirror);

                    for (int y = 0; y <= maxY - minY; y++)
                    {
                        int destidx = (element.Y1 + deltaY + y - Top) * Width + (element.X1 + deltaX - Left);
                        for (int x = 0; x <= maxX - minX; x++)
                        {
                            Image[destidx] = decodedImage[y + deltaY, x + deltaX];
                            destidx++;
                        }
                    }
                }
                else
                {

                }
            }
        }

        /// <summary>
        /// Saves the IndexedImage to the underlying DataFile
        /// </summary>
        public override void SaveImage()
        {
            // Additional copy is necessary for the case where the image pixels are not completely element-aligned
            // Edited image is merged into a full arranger image and then the entire arranger is encoded/saved

            var fullImage = Arranger.CopyPixelsIndexed().Image;
            var buffer = new byte[Arranger.ElementPixelSize.Height, Arranger.ElementPixelSize.Width];

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    fullImage.Image[(y + Top) * fullImage.Width + x + Left] = Image[y * Width + x];

            foreach (var el in Arranger.EnumerateElements().OfType<ArrangerElement>().Where(x => x.Codec is IIndexedCodec))
            {
                fullImage.Image.CopyToArray2D(el.X1, el.Y1, fullImage.Width, buffer, 0, 0, Arranger.ElementPixelSize.Width, Arranger.ElementPixelSize.Height);
                var codec = el.Codec as IIndexedCodec;

                buffer.InverseMirrorArray2D(el.Mirror);
                buffer.InverseRotateArray2D(el.Rotation);

                var encodedImage = codec.EncodeElement(el, buffer);
                codec.WriteElement(el, encodedImage);
            }

            foreach (var fs in Arranger.EnumerateElements().OfType<ArrangerElement>().Select(x => x.DataFile?.Stream).OfType<Stream>().Distinct())
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

            if (Arranger.GetElementAtPixel(x, y) is ArrangerElement el)
            {
                var codecColors = 1 << el.Codec.ColorDepth;
                var pal = el.Palette;

                if (color >= pal.Entries)
                    throw new ArgumentOutOfRangeException($"{nameof(SetPixel)} ({nameof(color)} ({color}): exceeded the number of entries in palette '{pal.Name}' ({pal.Entries})");
                if (color >= codecColors)
                    throw new ArgumentOutOfRangeException($"{nameof(SetPixel)} ({nameof(color)} ({color}): index exceeded the max number of colors in codec '{el.Codec.Name}' ({codecColors})");

                Image[x + Width * y] = color;
            }
            else
                throw new InvalidOperationException($"{nameof(SetPixel)} cannot set a pixel on the undefined {nameof(ArrangerElement)} at ({x}, {y})");
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
            var el = Arranger.GetElementAtPixel(x + Left, y + Top);

            if (el?.Palette is Palette pal)
            {
                var index = pal.GetIndexByNativeColor(color, ColorMatchStrategy.Exact);
                Image[x + Width * y] = index;
            }
            else
                throw new InvalidOperationException($"{nameof(SetPixel)} cannot set pixel at ({x}, {y}) because there is no associated palette");
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

            var el = Arranger.GetElementAtPixel(x + Left, y + Top);

            if (el is ArrangerElement element)
            {
                if (!(element.Codec is IIndexedCodec))
                    return new MagitekResult.Failed($"Cannot set pixel at ({x}, {y}) because the element's codec is not an indexed color type");

                var pal = element.Palette;

                if (pal is null)
                    return new MagitekResult.Failed($"Cannot set pixel at ({x}, {y}) because arranger '{Arranger.Name}' has no palette specified for the element");

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
        public ColorRgba32 GetPixelColor(int x, int y)
        {
            var el = Arranger.GetElementAtPixel(x + Left, y + Top);

            if (el?.Palette is Palette pal)
            {
                var palIndex = Image[x + Width * y];
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
        public MagitekResult TrySetPalette(int x, int y, Palette pal)
        {
            if (x + Left >= Arranger.ArrangerPixelSize.Width || y + Top >= Arranger.ArrangerPixelSize.Height)
                return new MagitekResult.Failed($"Cannot assign the palette because the location ({x}, {y}) is outside of the arranger " +
                    $"'{Arranger.Name}' bounds  ({Arranger.ArrangerPixelSize.Width}, {Arranger.ArrangerPixelSize.Height})");

            var el = Arranger.GetElementAtPixel(x + Left, y + Top);

            if (el is ArrangerElement element)
            {
                if (ReferenceEquals(pal, element.Palette))
                    return MagitekResult.SuccessResult;

                int maxIndex = 0;

                for (int pixelY = element.Y1; pixelY <= element.Y2; pixelY++)
                    for (int pixelX = element.X1; pixelX <= element.X2; pixelX++)
                        maxIndex = Math.Max(maxIndex, GetPixel(pixelX, pixelY));

                if (maxIndex < pal.Entries)
                {
                    var location = Arranger.PointToElementLocation(new Point(x + Left, y + Top));

                    element = element.WithPalette(pal);

                    Arranger.SetElement(element, location.X, location.Y);
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
        public bool FloodFill(int x, int y, byte fillIndex)
        {
            bool isModified = false;
            var replaceIndex = GetPixel(x, y);
            var startingPalette = GetElementAtPixel(x, y).Value.Palette;

            if (fillIndex == replaceIndex)
                return false;

            var openNodes = new Stack<(int x, int y)>();
            openNodes.Push((x, y));

            while (openNodes.Count > 0)
            {
                var nodePosition = openNodes.Pop();

                if (nodePosition.x >= 0 && nodePosition.x < Width && nodePosition.y >= 0 && nodePosition.y < Height)
                {
                    var nodeColor = GetPixel(nodePosition.x, nodePosition.y);
                    if (nodeColor == replaceIndex)
                    {
                        var destPalette = GetElementAtPixel(nodePosition.x, nodePosition.y).Value.Palette;
                        if (ReferenceEquals(startingPalette, destPalette))
                        {
                            isModified = true;
                            SetPixel(nodePosition.x, nodePosition.y, fillIndex);
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

        /// <summary>
        /// Remaps the colors of the image to new colors
        /// </summary>
        /// <param name="remap">List containing remapped indices</param>
        public void RemapColors(IList<byte> remap) =>
            Image = Image.Select(x => remap[x]).ToArray();
    }
}