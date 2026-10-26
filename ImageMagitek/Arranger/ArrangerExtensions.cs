using System;
using System.Drawing;
using System.Linq;

namespace ImageMagitek;

/// <summary>
/// Contains Arranger utility functions more relevant for GUI features
/// </summary>
public static class ArrangerExtensions
{
    /// <summary>
    /// Copies all elements within the specified arranger
    /// </summary>
    /// <param name="arranger">Source to copy from</param>
    /// <returns></returns>
    public static ElementCopy CopyElements(this IArrangerView arranger)
    {
        int width = arranger.ArrangerElementSize.Width;
        int height = arranger.ArrangerElementSize.Height;
        return arranger.CopyElements(0, 0, width, height);
    }

    /// <summary>
    /// Copies elements within the specified arranger
    /// </summary>
    /// <param name="arranger">Source to copy from</param>
    /// <param name="x">x-coordinate in element coordinates</param>
    /// <param name="y">y-coordinate in element coordinates</param>
    /// <param name="copyWidth">Width of copy in element coordinates</param>
    /// <param name="copyHeight">Height of copy in element coordinates</param>
    /// <returns>A <see cref="TilemapElementCopy"/> that also carries the entries when <paramref name="arranger"/> is a <see cref="Tilemaps.TilemapView"/></returns>
    public static ElementCopy CopyElements(this IArrangerView arranger, int x, int y, int copyWidth, int copyHeight)
    {
        if (arranger is Tilemaps.TilemapView view)
            return new TilemapElementCopy(view, x, y, copyWidth, copyHeight);

        return new ElementCopy(arranger, x, y, copyWidth, copyHeight);
    }

    /// <summary>
    /// Copies all pixels within the specified arranger
    /// </summary>
    /// <param name="arranger">Source to copy from</param>
    /// <returns></returns>
    public static IndexedPixelCopy CopyPixelsIndexed(this IArrangerView arranger)
    {
        int width = arranger.ArrangerPixelSize.Width;
        int height = arranger.ArrangerPixelSize.Height;
        return arranger.CopyPixelsIndexed(0, 0, width, height);
    }

    /// <summary>
    /// Copies pixels within the specified arranger
    /// </summary>
    /// <param name="arranger">Source to copy from</param>
    /// <param name="x">x-coordinate in pixel coordinates</param>
    /// <param name="y">y-coordinate in pixel coordinates</param>
    /// <param name="width">Width of copy in pixel coordinates</param>
    /// <param name="height">Height of copy in pixel coordinates</param>
    /// <returns></returns>
    public static IndexedPixelCopy CopyPixelsIndexed(this IArrangerView arranger, int x, int y, int width, int height)
    {
        if (arranger.ColorType == PixelColorType.Indexed)
        {
            return new IndexedPixelCopy(arranger, x, y, width, height);
        }
        else
        {
            throw new ArgumentException($"{nameof(CopyPixelsIndexed)}: Cannot copy from arranger '{arranger.Name}' with {nameof(PixelColorType)} '{arranger.ColorType}'");
        }
    }

    /// <summary>
    /// Copies all pixels within the specified arranger
    /// </summary>
    /// <param name="arranger">Source to copy from</param>
    /// <returns></returns>
    public static DirectPixelCopy CopyPixelsDirect(this IArrangerView arranger)
    {
        int width = arranger.ArrangerPixelSize.Width;
        int height = arranger.ArrangerPixelSize.Height;
        return arranger.CopyPixelsDirect(0, 0, width, height);
    }

    /// <summary>
    /// Copies pixels within the specified arranger
    /// </summary>
    /// <param name="arranger">Source to copy from</param>
    /// <param name="x">x-coordinate in pixel coordinates</param>
    /// <param name="y">y-coordinate in pixel coordinates</param>
    /// <param name="width">Width of copy in pixel coordinates</param>
    /// <param name="height">Height of copy in pixel coordinates</param>
    /// <returns></returns>
    public static DirectPixelCopy CopyPixelsDirect(this IArrangerView arranger, int x, int y, int width, int height)
    {
        if (arranger.ColorType == PixelColorType.Indexed)
        {
            throw new NotImplementedException($"{nameof(CopyPixelsDirect)}: Cannot copy from arranger '{arranger.Name}' with {nameof(PixelColorType)} '{arranger.ColorType}'");
        }
        else if (arranger.ColorType == PixelColorType.Direct)
        {
            return new DirectPixelCopy(arranger, x, y, width, height);
        }
        else
        {
            throw new ArgumentException($"{nameof(CopyPixelsIndexed)}: Cannot copy from arranger '{arranger.Name}' with {nameof(PixelColorType)} '{arranger.ColorType}'");
        }
    }

    /// <summary>
    /// Moves a Sequential Arranger's file position and updates each Element
    /// Will not move outside of the bounds of the underlying file
    /// </summary>
    /// <param name="arranger"></param>
    /// <param name="moveType">Type of move requested</param>
    /// <returns>Updated address of the first element</returns>
    public static BitAddress Move(this SequentialArranger arranger, ArrangerMoveType moveType)
    {
        if (arranger.Mode != ArrangerMode.Sequential)
            throw new InvalidOperationException($"{nameof(Move)}: Arranger {arranger.Name} is not in sequential mode");

        var initialAddress = arranger.Address;
        var newAddress = initialAddress;
        long bitDelta = 0;

        var patternWidth = arranger.TileLayout.Width;
        var patternHeight = arranger.TileLayout.Height;

        switch (moveType) // Calculate the new address based on the movement command. Negative and post-EOF addresses are handled after the switch
        {
            case ArrangerMoveType.ByteDown:
                newAddress += 8;
                break;
            case ArrangerMoveType.ByteUp:
                newAddress -= 8;
                break;
            case ArrangerMoveType.RowDown:
                if (arranger.Layout == ElementLayout.Tiled)
                    bitDelta = arranger.ArrangerElementSize.Width * arranger.ActiveCodec.StorageSize * patternHeight;
                else if (arranger.Layout == ElementLayout.Single)
                    bitDelta = arranger.ActiveCodec.StorageSize / arranger.ArrangerPixelSize.Height;

                newAddress += bitDelta;
                break;
            case ArrangerMoveType.RowUp:
                if (arranger.Layout == ElementLayout.Tiled)
                    bitDelta = arranger.ArrangerElementSize.Width * arranger.ActiveCodec.StorageSize * patternHeight;
                else if (arranger.Layout == ElementLayout.Single)
                    bitDelta = arranger.ActiveCodec.StorageSize / arranger.ArrangerPixelSize.Height;
                newAddress -= bitDelta;
                break;
            case ArrangerMoveType.ColRight:
                if (arranger.Layout == ElementLayout.Tiled)
                    bitDelta = arranger.ActiveCodec.StorageSize * patternWidth * patternHeight;
                else if (arranger.Layout == ElementLayout.Single)
                    bitDelta = 16 * arranger.ActiveCodec.StorageSize * arranger.ActiveCodec.Height / arranger.ActiveCodec.Width;
                newAddress += bitDelta;
                break;
            case ArrangerMoveType.ColLeft:
                if (arranger.Layout == ElementLayout.Tiled)
                    bitDelta = arranger.ActiveCodec.StorageSize * patternWidth * patternHeight;
                else if (arranger.Layout == ElementLayout.Single)
                    bitDelta = 16 * arranger.ActiveCodec.StorageSize * arranger.ActiveCodec.Height / arranger.ActiveCodec.Width;
                newAddress -= bitDelta;
                break;
            case ArrangerMoveType.PageDown:
                if (arranger.Layout == ElementLayout.Tiled)
                    bitDelta = arranger.ArrangerElementSize.Width * arranger.ActiveCodec.StorageSize * arranger.ArrangerElementSize.Height / 2;
                else if (arranger.Layout == ElementLayout.Single)
                    bitDelta = arranger.ActiveCodec.StorageSize / 2;
                newAddress += bitDelta;
                break;
            case ArrangerMoveType.PageUp:
                if (arranger.Layout == ElementLayout.Tiled)
                    bitDelta = arranger.ArrangerElementSize.Width * arranger.ActiveCodec.StorageSize * arranger.ArrangerElementSize.Height / 2;
                else if (arranger.Layout == ElementLayout.Single)
                    bitDelta = arranger.ActiveCodec.StorageSize / 2;
                newAddress -= bitDelta;
                break;
            case ArrangerMoveType.Home:
                newAddress = BitAddress.Zero;
                break;
            case ArrangerMoveType.End:
                newAddress = new BitAddress(arranger.FileSize * 8 - arranger.ArrangerBitSize);
                break;
        }

        var maxAddress = new BitAddress(arranger.FileSize * 8 - arranger.ArrangerBitSize);
        if (maxAddress.Offset < 0)
            maxAddress = BitAddress.Zero;

        newAddress = new BitAddress(Math.Clamp(newAddress.Offset, 0, maxAddress.Offset));

        if (initialAddress != newAddress)
            newAddress = arranger.Move(newAddress);

        return newAddress;
    }

    public static MagitekResult TryRotateElement(this Arranger arranger, int elementX, int elementY, RotationOperation rotate)
    {
        if (rotate == RotationOperation.None)
            return MagitekResult.SuccessResult;

        if (elementX > arranger.ArrangerElementSize.Width || elementY > arranger.ArrangerElementSize.Height)
            return new MagitekResult.Failed($"Location ({elementX}, {elementY}) is outside of the arranger");

        if (arranger.GetElement(elementX, elementY) is { } el)
        {
            if (el.Width != el.Height)
                return new MagitekResult.Failed("Only square elements may be rotated");

            // A single-axis mirror reverses the displayed turn direction (M∘Q∘M = Q⁻¹)
            if (el.Mirror is MirrorOperation.Horizontal or MirrorOperation.Vertical)
            {
                rotate = rotate switch
                {
                    RotationOperation.Left => RotationOperation.Right,
                    RotationOperation.Right => RotationOperation.Left,
                    _ => rotate
                };
            }

            var newRotation = (el.Rotation, rotate) switch
            {
                (RotationOperation.Left, RotationOperation.Left) => RotationOperation.Turn,
                (RotationOperation.Turn, RotationOperation.Left) => RotationOperation.Right,
                (RotationOperation.Right, RotationOperation.Left) => RotationOperation.None,

                (RotationOperation.Left, RotationOperation.Right) => RotationOperation.None,
                (RotationOperation.Turn, RotationOperation.Right) => RotationOperation.Left,
                (RotationOperation.Right, RotationOperation.Right) => RotationOperation.Turn,

                (RotationOperation.Left, RotationOperation.Turn) => RotationOperation.Right,
                (RotationOperation.Turn, RotationOperation.Turn) => RotationOperation.None,
                (RotationOperation.Right, RotationOperation.Turn) => RotationOperation.Left,

                (RotationOperation.None, _) => rotate,
                (_, RotationOperation.None) => el.Rotation,
                _ => RotationOperation.None
            };

            var rotatedElement = el.WithRotation(newRotation);
            arranger.SetElement(rotatedElement, elementX, elementY);

            return MagitekResult.SuccessResult;
        }

        return new MagitekResult.Failed($"No element present at position ({elementX}, {elementY}) to be rotated");
    }

    public static MagitekResult TryMirrorElement(this Arranger arranger, int elementX, int elementY, MirrorOperation mirror)
    {
        if (mirror == MirrorOperation.None)
            return MagitekResult.SuccessResult;

        if (arranger.GetElement(elementX, elementY) is { } el)
        {
            var mirroredElement = el.WithMirror(el.Mirror.Combine(mirror));
            arranger.SetElement(mirroredElement, elementX, elementY);

            return MagitekResult.SuccessResult;
        }

        return new MagitekResult.Failed($"No element present at position ({elementX}, {elementY}) to be mirrored");
    }

    /// <summary>
    /// True when any element uses a codec that cannot encode or reads a read-only data source, so the arranger's pixels cannot be saved
    /// </summary>
    public static bool IsReadOnly(this IArrangerView arranger) => arranger.GetReadOnlyReason() is not null;

    /// <summary>
    /// Why the arranger's pixels are read-only, naming the first offending element's codec or data file, or null when it is writable.
    /// A tilemap or its working view is also read-only when its tile source is; whether its map entries can be written is <see cref="TilemapArranger.MapReadOnlyReason"/>.
    /// </summary>
    /// <returns>A reason that completes "is read-only because it ..."</returns>
    public static string? GetReadOnlyReason(this IArrangerView arranger)
    {
        foreach (var element in arranger.EnumerateElements().OfType<ArrangerElement>())
        {
            if (!element.Codec.CanEncode)
                return $"uses codec '{element.Codec.Name}' that cannot encode";

            if (DescribeReadOnlySource(element.Source) is { } sourceReason)
                return sourceReason;
        }

        return FindTilemap(arranger) is { } tilemap ? DescribeReadOnlySource(tilemap.TileSource) : null;
    }

    private static TilemapArranger? FindTilemap(IArrangerView view) => view switch
    {
        TilemapArranger tilemap => tilemap,
        Tilemaps.TilemapView working => working.Tilemap,
        _ => null
    };

    /// <returns>A reason that completes "is read-only because it ...", or null when the source can be written</returns>
    internal static string? DescribeReadOnlySource(DataSource source) => source switch
    {
        CompressedDataSource compressed => $"reads compressed data '{compressed.Name}', which is read-only",
        { IsReadOnly: true } => $"reads data file '{source.Name}', which is read-only",
        _ => null
    };

    /// <summary>
    /// The first missing file among the arranger's element sources and its referenced palettes' sources, or null when none is missing
    /// </summary>
    public static FileDataSource? FindMissingDataSource(this IArrangerView arranger)
    {
        var tilemap = FindTilemap(arranger);
        return arranger.EnumerateElements().OfType<ArrangerElement>().Select(x => (DataSource?)x.Source)
            .Concat(arranger.GetReferencedPalettes().Select(x => x.DataSource))
            .Append(tilemap?.MapSource)
            .Append(tilemap?.TileSource)
            .Select(x => x is CompressedDataSource compressed ? compressed.Parent : x)
            .OfType<FileDataSource>()
            .Distinct()
            .FirstOrDefault(x => x.IsMissing);
    }

    /// <summary>
    /// Translates a point to an element location in the underlying arranger
    /// </summary>
    /// <param name="arranger"></param>
    /// <param name="location">Point in pixel coordinates</param>
    /// <returns>Element location in element coordinates</returns>
    public static Point PointToElementLocation(this IArrangerView arranger, Point location)
    {
        if (location.X < 0 || location.X >= arranger.ArrangerPixelSize.Width || location.Y < 0 || location.Y >= arranger.ArrangerPixelSize.Height)
            throw new ArgumentOutOfRangeException($"{nameof(PointToElementLocation)} Location ({location.X}, {location.Y}) is out of range");

        int elX = location.X / arranger.ElementPixelSize.Width;
        int elY = location.Y / arranger.ElementPixelSize.Height;

        return new Point(elX, elY);
    }
}
