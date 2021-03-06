﻿using System;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek
{
    public class DirectImage : ImageBase<ColorRgba32>
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

            foreach (var el in Arranger.EnumerateElements().OfType<ArrangerElement>().Where(x => x.DataFile is object))
            {
                if (el.Codec is IDirectCodec codec)
                {
                    var encodedBuffer = codec.ReadElement(el);

                    // TODO: Detect reads past end of file gracefully
                    if (encodedBuffer.Length == 0)
                        continue;

                    var decodeResult = codec.DecodeElement(el, encodedBuffer);

                    for (int y = 0; y < Arranger.ElementPixelSize.Height; y++)
                    {
                        var destidx = y * Width;
                        for (int x = 0; x < Arranger.ElementPixelSize.Width; x++)
                        {
                            Image[destidx] = decodeResult[x, y];
                            destidx++;
                        }
                    }
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
    }
}
