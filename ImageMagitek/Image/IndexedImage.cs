using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek
{
    public class IndexedImage : ImageBase<byte>
    {
        private Palette _defaultPalette;

        public IndexedImage(Arranger arranger) : this(arranger, null) { }

        public IndexedImage(Arranger arranger, Palette defaultPalette)
        {
            if (arranger is null)
                throw new ArgumentNullException($"{nameof(IndexedImage)}.Ctor parameter '{nameof(arranger)}' was null");

            Arranger = arranger;
            Image = new byte[Width * Height];
            _defaultPalette = defaultPalette;
            Render();
        }

        public override void ExportImage(string imagePath, IImageFileAdapter adapter) =>
            adapter.SaveImage(Image, Arranger, _defaultPalette, imagePath);

        public override void ImportImage(string imagePath, IImageFileAdapter adapter)
        {
            var importImage = adapter.LoadImage(imagePath, Arranger, _defaultPalette);
            importImage.CopyTo(Image, 0);
        }

        public override void Render()
        {
            if (Width <= 0 || Height <= 0)
                throw new InvalidOperationException($"{nameof(Render)}: arranger dimensions for '{Arranger.Name}' are too small to render " +
                    $"({Width}, {Height})");

            if (Width * Height != Image.Length)
                Image = new byte[Width * Height];

            foreach (var el in Arranger.EnumerateElements())
            {
                if (el.Codec is IIndexedCodec codec)
                {
                    var encodedBuffer = codec.ReadElement(el);
                    var decodedImage = codec.DecodeElement(el, encodedBuffer);

                    for (int y = 0; y < Arranger.ElementPixelSize.Height; y++)
                    {
                        var destidx = (y + el.Y1) * Width + el.X1;
                        for (int x = 0; x < Arranger.ElementPixelSize.Width; x++)
                        {
                            Image[destidx] = decodedImage[x, y];
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
        /// <param name="x">x-coordinate in pixel coordinates</param>
        /// <param name="y">y-coordinate in pixel coordinates</param>
        /// <param name="color">Palette index of color</param>
        public override void SetPixel(int x, int y, byte color)
        {
            if (x >= Width || y >= Height || x < 0 || y < 0)
                throw new ArgumentOutOfRangeException($"{nameof(SetPixel)} ({nameof(x)}: {x}, {nameof(y)}: {y}) were outside the image bounds ({nameof(Width)}: {Width}, {nameof(Height)}: {Height}");

            var pal = Arranger.GetElement(x, y).Palette;
            if (color >= pal.Entries)
                throw new ArgumentOutOfRangeException($"{nameof(SetPixel)} ({nameof(color)} ({color}): exceeded the number of entries in palette '{pal.Name}' ({pal.Entries})");

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
            var elem = Arranger.GetElementAtPixel(x, y);
            if (!(elem.Codec is IIndexedCodec))
                return new MagitekResult.Failed($"Failed to set pixel at ({x}, {y}) because the element's codec is not an indexed color type");

            var pal = elem.Palette ?? _defaultPalette;

            if (pal is null)
                return new MagitekResult.Failed($"Failed to set pixel at ({x}, {y}) because arranger '{Arranger.Name}' has no palette specified and no default palette");

            if (!pal.ContainsNativeColor(color))
                return new MagitekResult.Failed($"Failed to set pixel at ({x}, {y}) because the palette '{pal.Name}' does not contain the native color ({color.R}, {color.G}, {color.B}, {color.A})");

            var index = pal.GetIndexByNativeColor(color, true);
            Image[x + Width * y] = index;
            return new MagitekResult.Success();
        }

        /// <summary>
        /// Gets the pixel's native color at the specified pixel coordinate
        /// </summary>
        /// <param name="x">x-coordinate in pixel coordinates</param>
        /// <param name="y">y-coordinate in pixel coordinates</param>
        /// <param name="arranger"></param>
        public ColorRgba32 GetPixel(int x, int y, Arranger arranger)
        {
            var pal = Arranger.GetElementAtPixel(x, y).Palette ?? _defaultPalette;
            var palIndex = Image[x + Width * y];
            return pal[palIndex];
        }

        /// <summary>
        /// Remaps the colors of the image to new colors
        /// </summary>
        /// <param name="remap">List containing remapped indices</param>
        public void RemapColors(IList<byte> remap) =>
            Image = Image.Select(x => remap[x]).ToArray();
    }
}