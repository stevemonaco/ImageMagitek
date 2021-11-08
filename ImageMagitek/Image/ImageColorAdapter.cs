using System;
using ImageMagitek.Colors;

namespace ImageMagitek;

public static class ImageColorAdapter
{
    /// <summary>
    /// Creates a new IndexedImage from a DirectImage
    /// </summary>
    /// <param name="direct">The source DirectImage</param>
    /// <param name="pal">The palette for color conversion</param>
    /// <returns></returns>
    public static IndexedImage ToIndexed(DirectImage direct, Palette pal)
    {
        throw new NotImplementedException();

        //var indexed = new IndexedImage(direct.Width, direct.Height);

        //for(int i = 0; i < direct.Width * direct.Height; i++)
        //{
        //    var index = pal.GetIndexByNativeColor(direct.Image[i], true);
        //    indexed.Image[i] = index;
        //}

        //return indexed;
    }

    /// <summary>
    /// Creates a new IndexedImage from a DirectImage
    /// </summary>
    /// <param name="direct">The source DirectImage</param>
    /// <param name="arranger">The arranger containing palettes for color conversion</param>
    /// <returns></returns>
    public static IndexedImage ToIndexed(DirectImage direct, Arranger arranger)
    {
        throw new NotImplementedException();
        //if (direct.Width != arranger.ArrangerPixelSize.Width || direct.Height != arranger.ArrangerPixelSize.Height)
        //    throw new ArgumentException($"{nameof(ToIndexed)} must have matched image dimensions. " +
        //        $"{nameof(DirectImage)}: ({direct.Width}, {direct.Height})" +
        //        $"{arranger.Name}: ({arranger.ArrangerPixelSize.Width}, {arranger.ArrangerPixelSize.Height})");

        //var indexed = new IndexedImage(arranger);

        //for (int y = 0; y < direct.Height; y++)
        //{
        //    for (int x = 0; x < direct.Width; x++)
        //    {
        //        var pal = arranger.GetElement(x, y).Palette;
        //        var color = pal.GetIndexByNativeColor(direct.GetPixel(x, y), true);
        //        indexed.SetPixel(x, y, color);
        //    }
        //}

        //return indexed;
    }

    /// <summary>
    /// Creates a new DirectImage from an IndexedImage
    /// </summary>
    /// <param name="indexed">The source IndexedImage</param>
    /// <param name="pal">The palette for color conversion</param>
    /// <returns></returns>
    public static DirectImage ToDirect(IndexedImage indexed, Palette pal)
    {
        throw new NotImplementedException();
        //var direct = new DirectImage(indexed.Width, indexed.Height);

        //for(int i = 0; i < indexed.Width * indexed.Height; i++)
        //{
        //    var color = pal.GetNativeColor(indexed.Image[i]);
        //    direct.Image[i] = color;
        //}

        //return direct;
    }

    /// <summary>
    /// Creates a new DirectImage from an IndexedImage
    /// </summary>
    /// <param name="indexed">The source IndexedImage</param>
    /// <param name="arranger">The arranger containing palettes for color conversion</param>
    /// <returns></returns>
    public static DirectImage ToDirect(IndexedImage indexed, Arranger arranger)
    {
        throw new NotImplementedException();
        //if (indexed.Width != arranger.ArrangerPixelSize.Width || indexed.Height != arranger.ArrangerPixelSize.Height)
        //    throw new ArgumentException($"{nameof(ToIndexed)} must have matched image dimensions. " +
        //        $"{nameof(DirectImage)}: ({indexed.Width}, {indexed.Height})" +
        //        $"{arranger.Name}: ({arranger.ArrangerPixelSize.Width}, {arranger.ArrangerPixelSize.Height})");

        //var direct = new DirectImage(indexed.Width, indexed.Height);

        //for (int y = 0; y < direct.Height; y++)
        //{
        //    for (int x = 0; x < direct.Width; x++)
        //    {
        //        var pal = arranger.GetElement(x, y).Palette;
        //        var color = pal[indexed.GetPixel(x, y)];
        //        direct.SetPixel(x, y, color);
        //    }
        //}

        //return direct;
    }
}
