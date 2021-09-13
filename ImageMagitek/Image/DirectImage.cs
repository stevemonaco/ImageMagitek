using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek
{
    public sealed class DirectImage : ImageBase<ColorRgba32>
    {
        public DirectImage(Arranger arranger) :
            this(arranger, 0, 0, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height)
        {
        }

        /// <summary>
        /// Creates a DirectImage with a subsection of an Arranger
        /// </summary>
        /// <param name="arranger">Source Arranger</param>
        /// <param name="x">Left-edge of subsection in pixel coordinates</param>
        /// <param name="y">Top-edge of subsection in pixel coordinates</param>
        /// <param name="width">Width of subsection in pixel coordinates</param>
        /// <param name="height">Height of subsection in pixel coordinates</param>
        public DirectImage(Arranger arranger, int x, int y, int width, int height)
        {
            if (arranger is null)
                throw new ArgumentNullException($"{nameof(DirectImage)}.Ctor parameter '{nameof(arranger)}' was null");

            if (arranger.ColorType != PixelColorType.Direct)
                throw new ArgumentException($"{nameof(DirectImage)}.Ctor: Arranger '{arranger.Name}' has an invalid color type '{arranger.ColorType}'");

            Arranger = arranger;
            Left = x;
            Top = y;
            Width = width;
            Height = height;

            Image = new ColorRgba32[Width * Height];
            Render();
        }

        public override void ExportImage(string imagePath, IImageFileAdapter adapter) =>
            adapter.SaveImage(Image, Width, Height, imagePath);

        public void ImportImage(string imagePath, IImageFileAdapter adapter)
        {
            var importImage = adapter.LoadImage(imagePath);
            importImage.CopyTo(Image, 0);
        }

        public override void Render()
        {
            if (Width <= 0 || Height <= 0)
                throw new InvalidOperationException($"{nameof(Render)}: arranger dimensions for '{Arranger.Name}' are too small to render " +
                    $"({Width}, {Height})");

            if (Width * Height != Image.Length)
                Image = new ColorRgba32[Width * Height];

            // TODO: Handle undefined elements explicitly and clear image subsections
            Array.Clear(Image, 0, Image.Length);

            var locations = Arranger.EnumerateElementLocationsByPixel(Left, Top, Width, Height);

            Rectangle imageRect = new Rectangle(Left, Top, Width, Height);

            foreach (var location in locations)
            {
                var el = Arranger.GetElement(location.X, location.Y);
                if (el is ArrangerElement element && element.Codec is IDirectCodec codec)
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

        public override void SaveImage()
        {
            var buffer = new ColorRgba32[Arranger.ElementPixelSize.Width, Arranger.ElementPixelSize.Height];
            foreach (var el in Arranger.EnumerateElements().OfType<ArrangerElement>().Where(x => x.Codec is IDirectCodec))
            {
                Image.CopyToArray2D(buffer, el.X1, el.Y1, Width, el.Width, el.Height);
                var codec = el.Codec as IDirectCodec;

                var encodeResult = codec.EncodeElement(el, buffer);
                codec.WriteElement(el, encodeResult);
            }
            foreach (var fs in Arranger.EnumerateElements().OfType<ArrangerElement>().Select(x => x.DataFile.Stream).Distinct())
                fs.Flush();
        }

        /// <summary>
        /// Fills the surrounding, contiguous color area with a new color
        /// </summary>
        /// <param name="x">x-coordinate to start at in pixel coordinates</param>
        /// <param name="y">y-coordinate to start at in pixel coordinates</param>
        /// <param name="fillIndex">Palette index to fill with</param>
        /// <returns>True if any pixels were modified</returns>
        public bool FloodFill(int x, int y, ColorRgba32 fillColor)
        {
            bool isModified = false;
            var replaceColor = GetPixel(x, y);

            if (fillColor.Color == replaceColor.Color)
                return false;

            var openNodes = new Stack<(int x, int y)>();
            openNodes.Push((x, y));

            while (openNodes.Count > 0)
            {
                var nodePosition = openNodes.Pop();

                if (nodePosition.x >= 0 && nodePosition.x < Width && nodePosition.y >= 0 && nodePosition.y < Height)
                {
                    var nodeColor = GetPixel(nodePosition.x, nodePosition.y);
                    if (nodeColor.Color == replaceColor.Color)
                    {
                        isModified = true;
                        SetPixel(nodePosition.x, nodePosition.y, fillColor);
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
}
