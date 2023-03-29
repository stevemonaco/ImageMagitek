using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek;

/// <summary>
/// Provides functionality to work with pixel data of Arrangers with palettized graphics
/// </summary>
public sealed class IndexedImage : ImageBase<byte>
{
    public override Arranger Arranger { get; }
    public override byte[] Image { get; protected set; }

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
            throw new ArgumentNullException(nameof(arranger), $"{nameof(IndexedImage)}.Ctor parameter was null");

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

        if (result.Value is MagitekResult.Success && importImage is not null)
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

        Array.Clear(Image, 0, Image.Length);

        var locations = Arranger.EnumerateElementLocationsWithinPixelRange(Left, Top, Width, Height);

        var imageRect = new Rectangle(Left, Top, Width, Height);

        foreach (var location in locations)
        {
            var el = Arranger.GetElement(location.X, location.Y);
            if (el is ArrangerElement { Codec: IIndexedCodec codec } element)
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
            var codec = (IIndexedCodec)el.Codec;

            buffer.InverseMirrorArray2D(el.Mirror);
            buffer.InverseRotateArray2D(el.Rotation);

            var encodedImage = codec.EncodeElement(el, buffer);
            codec.WriteElement(el, encodedImage);
        }

        foreach (var df in Arranger.EnumerateElements().OfType<ArrangerElement>().Select(x => x.Source).Distinct())
            df.Flush();
    }

    /// <summary>
    /// Remaps the colors of the image to new colors
    /// </summary>
    /// <param name="remap">List containing remapped indices</param>
    public void RemapColors(IList<byte> remap) =>
        Image = Image.Select(x => remap[x]).ToArray();
}
