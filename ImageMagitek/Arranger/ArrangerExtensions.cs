using System;
using System.Drawing;

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
    public static ElementCopy CopyElements(this Arranger arranger)
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
    /// <returns></returns>
    public static ElementCopy CopyElements(this Arranger arranger, int x, int y, int copyWidth, int copyHeight)
    {
        return new ElementCopy(arranger, x, y, copyWidth, copyHeight);
    }

    /// <summary>
    /// Copies all pixels within the specified arranger
    /// </summary>
    /// <param name="arranger">Source to copy from</param>
    /// <returns></returns>
    public static IndexedPixelCopy CopyPixelsIndexed(this Arranger arranger)
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
    /// <param name="copyWidth">Width of copy in pixel coordinates</param>
    /// <param name="copyHeight">Height of copy in pixel coordinates</param>
    /// <returns></returns>
    public static IndexedPixelCopy CopyPixelsIndexed(this Arranger arranger, int x, int y, int width, int height)
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
    public static DirectPixelCopy CopyPixelsDirect(this Arranger arranger)
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
    /// <param name="copyWidth">Width of copy in pixel coordinates</param>
    /// <param name="copyHeight">Height of copy in pixel coordinates</param>
    /// <returns></returns>
    public static DirectPixelCopy CopyPixelsDirect(this Arranger arranger, int x, int y, int width, int height)
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
    /// <param name="moveType">Type of move requested</param>
    /// <returns>Updated address of first element</returns>
    public static BitAddress Move(this SequentialArranger arranger, ArrangerMoveType moveType)
    {
        if (arranger.Mode != ArrangerMode.Sequential)
            throw new InvalidOperationException($"{nameof(Move)}: Arranger {arranger.Name} is not in sequential mode");

        var initialAddress = arranger.Address;
        var newAddress = initialAddress;
        long bitDelta = 0;

        var patternWidth = arranger.TileLayout?.Width ?? 1;
        var patternHeight = arranger.TileLayout?.Height ?? 1;

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

        if (arranger.GetElement(elementX, elementY) is ArrangerElement el)
        {
            if (el.Width != el.Height)
                return new MagitekResult.Failed("Only square elements may be rotated");

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
                _ => el.Rotation
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

        if (arranger.GetElement(elementX, elementY) is ArrangerElement el)
        {
            var newMirror = (el.Mirror, mirror) switch
            {
                (MirrorOperation.Horizontal, MirrorOperation.Horizontal) => MirrorOperation.None,
                (MirrorOperation.Vertical, MirrorOperation.Horizontal) => MirrorOperation.Both,
                (MirrorOperation.Both, MirrorOperation.Horizontal) => MirrorOperation.Vertical,

                (MirrorOperation.Horizontal, MirrorOperation.Vertical) => MirrorOperation.Both,
                (MirrorOperation.Vertical, MirrorOperation.Vertical) => MirrorOperation.None,
                (MirrorOperation.Both, MirrorOperation.Vertical) => MirrorOperation.Horizontal,

                (MirrorOperation.Horizontal, MirrorOperation.Both) => MirrorOperation.Vertical,
                (MirrorOperation.Vertical, MirrorOperation.Both) => MirrorOperation.Horizontal,
                (MirrorOperation.Both, MirrorOperation.Both) => MirrorOperation.None,

                (MirrorOperation.None, _) => mirror,
                (_, MirrorOperation.None) => el.Mirror,
                _ => el.Mirror
            };

            var mirroredElement = el.WithMirror(newMirror);
            arranger.SetElement(mirroredElement, elementX, elementY);

            return MagitekResult.SuccessResult;
        }

        return new MagitekResult.Failed($"No element present at position ({elementX}, {elementY}) to be mirrored");
    }

    /// <summary>
    /// Translates a point to an element location in the underlying arranger
    /// </summary>
    /// <param name="location">Point in pixel coordinates</param>
    /// <returns>Element location in element coordinates</returns>
    public static Point PointToElementLocation(this Arranger arranger, Point location)
    {
        if (location.X < 0 || location.X >= arranger.ArrangerPixelSize.Width || location.Y < 0 || location.Y >= arranger.ArrangerPixelSize.Height)
            throw new ArgumentOutOfRangeException($"{nameof(PointToElementLocation)} Location ({location.X}, {location.Y}) is out of range");

        int elX = location.X / arranger.ElementPixelSize.Width;
        int elY = location.Y / arranger.ElementPixelSize.Height;

        return new Point(elX, elY);
    }
}
