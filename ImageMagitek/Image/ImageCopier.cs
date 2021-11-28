using System.Drawing;
using System.Linq;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek.Image;

/// <summary>
/// Operations to remap pixels during copy operations
/// </summary>
public enum PixelRemapOperation
{
    /// <summary>The source index is directly applied to the destination</summary>
    RemapByExactIndex,
    /// <summary>The source index is translated to a palette color and matched exactly against the destination's available palette colors</summary>
    RemapByExactPaletteColors,
    /// <summary>Not yet implemented</summary>
    RemapByAnyIndex
}

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

        return MagitekResult.SuccessResult;
    }

    private static MagitekResult CanRemapByExactIndex(IndexedImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
    {
        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                var el = source.GetElementAtPixel(sourceStart.X + x, sourceStart.Y + y);
                if (el is ArrangerElement element)
                {
                    if ((1 << element.Codec.ColorDepth) < dest.GetPixel(destStart.X + x, destStart.Y + y))
                        return new MagitekResult.Failed($"Destination image contains a palette index too large to map to the source image pixels at destination position ({destStart.X + x}, {destStart.Y + y}) and source position ({sourceStart.X + x}, {sourceStart.Y + y})");
                }
            }
        }

        return MagitekResult.SuccessResult;
    }

    private static MagitekResult CanRemapByExactPaletteColors(IndexedImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
    {
        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                var color = source.GetPixelColor(x + sourceStart.X, y + sourceStart.Y);
                if (dest.CanSetPixel(x + destStart.X, y + destStart.Y, color).Value is MagitekResult.Failed)
                {
                    var el = dest.GetElementAtPixel(x + destStart.X, y + destStart.Y);

                    var palName = el?.Palette?.Name ?? "Default";
                    return new MagitekResult.Failed($"Destination image at (x: {destStart.X}, y: {destStart.Y}) with element palette '{palName}' could not be set to the source color ({color.A}, {color.R}, {color.G}, {color.B})");
                }
            }
        }

        return MagitekResult.SuccessResult;
    }

    private static MagitekResult CanRemapByExactPaletteColors(DirectImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
    {
        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                var color = source.GetPixel(x + sourceStart.X, y + sourceStart.Y);
                if (dest.CanSetPixel(x + destStart.X, y + destStart.Y, color).Value is MagitekResult.Failed)
                {
                    var palName = dest.GetElementAtPixel(x + destStart.X, y + destStart.Y)?.Palette?.Name ?? "Undefined";
                    return new MagitekResult.Failed($"Destination image at (x: {destStart.X}, y: {destStart.Y}) with element palette '{palName}' could not be set to the source color ({color.A}, {color.R}, {color.G}, {color.B})");
                }
            }
        }

        return MagitekResult.SuccessResult;
    }

    private static MagitekResult CanExactColorRemapPixels(DirectImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
    {
        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                var color = source.GetPixel(x + sourceStart.X, y + sourceStart.Y);
                var result = dest.CanSetPixel(x + destStart.X, y + destStart.Y, color);
                if (result.Value is MagitekResult.Failed)
                    return result;
            }
        }

        return MagitekResult.SuccessResult;
    }

    //private static MagitekResult CanRemapByAnyIndex(IndexedImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
    //{
    //    for (int y = 0; y < copyHeight; y++)
    //    {
    //        for (int x = 0; x < copyWidth; x++)
    //        {
    //        }
    //    }

    //    return MagitekResult.SuccessResult;
    //}

    private static void ApplyRemapByExactIndex(IndexedImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
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

    private static void ApplyRemapByExactPaletteColors(IndexedImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
    {
        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                var color = source.GetPixelColor(x + sourceStart.X, y + sourceStart.Y);
                dest.SetPixel(x + destStart.X, y + destStart.Y, color);
            }
        }
    }

    private static void ApplyRemapByExactPaletteColors(DirectImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
    {
        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                var color = source.GetPixel(x + sourceStart.X, y + sourceStart.Y);
                dest.SetPixel(x + destStart.X, y + destStart.Y, color);
            }
        }
    }

    private static void ApplyExactColorRemapPixels(DirectImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
    {
        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                var color = source.GetPixel(x + sourceStart.X, y + sourceStart.Y);
                dest.SetPixel(x + destStart.X, y + destStart.Y, color);
            }
        }
    }

    private static bool ImageRegionContainsInvalidElements<TPixel>(ImageBase<TPixel> image, Point start, int width, int height)
        where TPixel : struct
    {
        var elems = image.GetElementsByPixel(start.X, start.Y, width, height);
        return elems.Any(x => x is null);
    }

    public static MagitekResult CopyPixels(IndexedPixelCopy source, IndexedImage dest, Point destStart, params PixelRemapOperation[] operationAttempts)
    {
        return CopyPixels(source.Image, dest, new Point(0, 0), destStart, source.Width, source.Height, operationAttempts);
    }

    public static MagitekResult CopyPixels(IndexedImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight, params PixelRemapOperation[] operationAttempts)
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
            if (operation == PixelRemapOperation.RemapByExactIndex)
            {
                if (CanRemapByExactIndex(source, dest, sourceStart, destStart, copyWidth, copyHeight).Value is MagitekResult.Success)
                {
                    ApplyRemapByExactIndex(source, dest, sourceStart, destStart, copyWidth, copyHeight);
                    return MagitekResult.SuccessResult;
                }
            }
            else if (operation == PixelRemapOperation.RemapByExactPaletteColors)
            {
                if (CanRemapByExactPaletteColors(source, dest, sourceStart, destStart, copyWidth, copyHeight).Value is MagitekResult.Success)
                {
                    ApplyRemapByExactPaletteColors(source, dest, sourceStart, destStart, copyWidth, copyHeight);
                    return MagitekResult.SuccessResult;
                }
            }
            else if (operation == PixelRemapOperation.RemapByAnyIndex)
            {
                //if (CanRemapByAnyIndex(source, dest, sourceStart, destStart, copyWidth, copyHeight).Value is MagitekResult.Success)
                //{
                //    ApplyRemapByAnyIndex(source, dest, sourceStart, destStart, copyWidth, copyHeight);
                //    return MagitekResult.SuccessResult;
                //}
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

        return MagitekResult.SuccessResult;
    }

    public static MagitekResult CopyPixels(DirectImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight, params PixelRemapOperation[] operationAttempts)
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
            if (operation == PixelRemapOperation.RemapByExactIndex)
            {
                if (CanExactColorRemapPixels(source, dest, sourceStart, destStart, copyWidth, copyHeight).Value is MagitekResult.Success)
                {
                    ApplyExactColorRemapPixels(source, dest, sourceStart, destStart, copyWidth, copyHeight);
                    return MagitekResult.SuccessResult;
                }
            }
            else if (operation == PixelRemapOperation.RemapByExactPaletteColors)
            {
                if (CanRemapByExactPaletteColors(source, dest, sourceStart, destStart, copyWidth, copyHeight).Value is MagitekResult.Success)
                {
                    ApplyRemapByExactPaletteColors(source, dest, sourceStart, destStart, copyWidth, copyHeight);
                    return MagitekResult.SuccessResult;
                }
            }
            else if (operation == PixelRemapOperation.RemapByAnyIndex)
            {

            }
        }

        return new MagitekResult.Failed($"Image cannot be copied as no suitable copy method was found");
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

        return MagitekResult.SuccessResult;
    }
}
