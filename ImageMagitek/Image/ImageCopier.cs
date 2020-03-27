using ImageMagitek.Codec;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace ImageMagitek.Image
{
    public enum ImageCopyOperation { ExactIndex, RemapByPalette, RemapByAnyIndex }

    public static class ImageCopier
    {
        private static MagitekResult CanCopyPixelDimensions<TPixel1, TPixel2>(ImageBase<TPixel1> source, ImageBase<TPixel2> dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
            where TPixel1 : struct
            where TPixel2 : struct
        {
            if (copyWidth > (source.Width - sourceStart.X))
                return new MagitekResult.Failed($"Source image with width ({source.Width}) is insufficient to copy {copyWidth} pixels starting from position {sourceStart.X}");
            if (copyHeight > (source.Height - sourceStart.Y))
                return new MagitekResult.Failed($"Source image with height ({source.Height}) is insufficient to copy {copyHeight} pixels starting from position {sourceStart.Y}");

            if (copyWidth > (dest.Width - destStart.X))
                return new MagitekResult.Failed($"Destination image with width ({dest.Width}) is insufficient to copy {copyWidth} pixels starting from position {destStart.X}");
            if (copyHeight > (dest.Height - destStart.Y))
                return new MagitekResult.Failed($"Destination image with height ({dest.Height}) is insufficient to copy {copyHeight} pixels starting from position {destStart.Y}");

            return new MagitekResult.Success();
        }

        private static MagitekResult CanExactIndexRemapPixels(IndexedImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
        {
            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                {
                    var element = source.GetElementAtPixel(sourceStart.X + x, sourceStart.Y + y);
                    if ((1 << element.Codec.ColorDepth) < dest.GetPixel(destStart.X + x, destStart.Y + y))
                        return new MagitekResult.Failed($"Destination image contains a palette index too large to map to the source image pixels at destination position ({destStart.X + x}, {destStart.Y + y}) and source position ({sourceStart.X + x}, {sourceStart.Y + y})");
                }
            }

            return new MagitekResult.Success();
        }

        private static MagitekResult CanExactColorRemapPixels(DirectImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
        {
            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                {
                    var color = source.GetPixel(sourceStart.X + x, sourceStart.Y + y);
                    var result = dest.CanSetPixel(sourceStart.X + x, sourceStart.Y + y, color);
                    if (result.Value is MagitekResult.Failed)
                        return result;
                }
            }

            return new MagitekResult.Success();
        }

        private static void ApplyExactIndexRemapPixels(IndexedImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
        {
            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                {
                    var index = source.GetPixel(x + sourceStart.X, y + sourceStart.Y);
                    dest.SetPixel(x + destStart.X, y + destStart.Y, index);
                }
            }
        }

        private static void ApplyExactColorRemapPixels(DirectImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
        {
            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                {
                    var color = source.GetPixel(sourceStart.X + x, sourceStart.Y + y);
                    dest.SetPixel(sourceStart.X + x, sourceStart.Y + y, color);
                }
            }
        }

        private static bool ImageRegionContainsInvalidElements<TPixel>(ImageBase<TPixel> image, Point start, int width, int height)
            where TPixel : struct
        {
            var elems = image.GetElementsByPixel(start.X, start.Y, width, height);
            return elems.Any(x => x.Codec is BlankIndexedCodec || x.Codec is BlankDirectCodec);
        }

        public static MagitekResult CopyPixels(IndexedImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight, params ImageCopyOperation[] operationAttempts)
        {
            var dimensionResult = CanCopyPixelDimensions(source, dest, sourceStart, destStart, copyWidth, copyHeight);

            if (dimensionResult.Value is MagitekResult.Failed)
                return dimensionResult;

            if (ImageRegionContainsInvalidElements(source, sourceStart, copyWidth, copyHeight))
                return new MagitekResult.Failed($"Source image copy region contains blank elements");

            if (ImageRegionContainsInvalidElements(dest, destStart, copyWidth, copyHeight))
                return new MagitekResult.Failed($"Destination image paste region contains blank elements");

            foreach (var operation in operationAttempts)
            {
                if (operation == ImageCopyOperation.ExactIndex)
                {
                    if (CanExactIndexRemapPixels(source, dest, sourceStart, destStart, copyWidth, copyHeight).Value is MagitekResult.Success)
                    {
                        ApplyExactIndexRemapPixels(source, dest, sourceStart, destStart, copyWidth, copyHeight);
                        return new MagitekResult.Success();
                    }
                }
                else if (operation == ImageCopyOperation.RemapByPalette)
                {

                }
                else if (operation == ImageCopyOperation.RemapByAnyIndex)
                {

                }
            }

            return new MagitekResult.Failed($"Image cannot be copied as no suitable copy method was found");
        }

        public static MagitekResult CopyPixels(IndexedImage source, DirectImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
        {
            var dimensionResult = CanCopyPixelDimensions(source, dest, sourceStart, destStart, copyWidth, copyHeight);

            if (dimensionResult.Value is MagitekResult.Failed)
                return dimensionResult;

            if (ImageRegionContainsInvalidElements(source, sourceStart, copyWidth, copyHeight))
                return new MagitekResult.Failed($"Source image copy region contains blank elements");

            if (ImageRegionContainsInvalidElements(dest, destStart, copyWidth, copyHeight))
                return new MagitekResult.Failed($"Destination image paste region contains blank elements");

            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                {
                    var color = source.GetPixelColor(x + sourceStart.X, y + sourceStart.Y);
                    dest.SetPixel(x + destStart.X, y + destStart.Y, color);
                }
            }

            return new MagitekResult.Success();
        }

        public static MagitekResult CopyPixels(DirectImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight, params ImageCopyOperation[] operationAttempts)
        {
            var dimensionResult = CanCopyPixelDimensions(source, dest, sourceStart, destStart, copyWidth, copyHeight);

            if (dimensionResult.Value is MagitekResult.Failed)
                return dimensionResult;

            if (ImageRegionContainsInvalidElements(source, sourceStart, copyWidth, copyHeight))
                return new MagitekResult.Failed($"Source image copy region contains blank elements");

            if (ImageRegionContainsInvalidElements(dest, destStart, copyWidth, copyHeight))
                return new MagitekResult.Failed($"Destination image paste region contains blank elements");

            foreach (var operation in operationAttempts)
            {
                if (operation == ImageCopyOperation.ExactIndex)
                {
                    if (CanExactColorRemapPixels(source, dest, sourceStart, destStart, copyWidth, copyHeight).Value is MagitekResult.Success)
                    {
                        ApplyExactColorRemapPixels(source, dest, sourceStart, destStart, copyWidth, copyHeight);
                        return new MagitekResult.Success();
                    }
                }
                else if (operation == ImageCopyOperation.RemapByPalette)
                {

                }
                else if (operation == ImageCopyOperation.RemapByAnyIndex)
                {

                }
            }

            return new MagitekResult.Success();
        }

        public static MagitekResult CopyPixels(DirectImage source, DirectImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
        {
            var dimensionResult = CanCopyPixelDimensions(source, dest, sourceStart, destStart, copyWidth, copyHeight);

            if (dimensionResult.Value is MagitekResult.Failed)
                return dimensionResult;

            if (ImageRegionContainsInvalidElements(source, sourceStart, copyWidth, copyHeight))
                return new MagitekResult.Failed($"Source image copy region contains blank elements");

            if (ImageRegionContainsInvalidElements(dest, destStart, copyWidth, copyHeight))
                return new MagitekResult.Failed($"Destination image paste region contains blank elements");

            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                {
                    var color = source.GetPixel(x + sourceStart.X, y + sourceStart.Y);
                    dest.SetPixel(x + destStart.X, y + destStart.Y, color);
                }
            }

            return new MagitekResult.Success();
        }
    }
}
