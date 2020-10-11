using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using MoreLinq;

namespace ImageMagitek
{
    /// <summary>
    /// Contains Arranger utility functions more relevant for GUI features
    /// </summary>
    public static class ArrangerExtensions
    {
        public static ElementCopy CopyElements(this Arranger arranger)
        {
            int width = arranger.ArrangerElementSize.Width;
            int height = arranger.ArrangerElementSize.Height;
            return arranger.CopyElements(0, 0, width, height);
        }

        public static ElementCopy CopyElements(this Arranger arranger, int x, int y, int width, int height)
        {
            return new ElementCopy(arranger, x, y, width, height);
        }

        public static IndexedPixelCopy CopyPixelsIndexed(this Arranger arranger)
        {
            int width = arranger.ArrangerPixelSize.Width;
            int height = arranger.ArrangerPixelSize.Height;
            return arranger.CopyPixelsIndexed(0, 0, width, height);
        }

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

        public static DirectPixelCopy CopyPixelsDirect(this Arranger arranger)
        {
            int width = arranger.ArrangerPixelSize.Width;
            int height = arranger.ArrangerPixelSize.Height;
            return arranger.CopyPixelsDirect(0, 0, width, height);
        }

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
        public static FileBitAddress Move(this SequentialArranger arranger, ArrangerMoveType moveType)
        {
            if (arranger.Mode != ArrangerMode.Sequential)
                throw new InvalidOperationException($"{nameof(Move)}: Arranger {arranger.Name} is not in sequential mode");

            var initialAddress = arranger.GetInitialSequentialFileAddress();
            var newAddress = arranger.GetInitialSequentialFileAddress();
            FileBitAddress delta = 0;

            switch (moveType) // Calculate the new address based on the movement command. Negative and post-EOF addresses are handled after the switch
            {
                case ArrangerMoveType.ByteDown:
                    newAddress += 8;
                    break;
                case ArrangerMoveType.ByteUp:
                    newAddress -= 8;
                    break;
                case ArrangerMoveType.RowDown:
                    if(arranger.Layout == ArrangerLayout.Tiled)
                        delta = arranger.ArrangerElementSize.Width * arranger.ActiveCodec.StorageSize;
                    else if(arranger.Layout == ArrangerLayout.Single)
                        delta = arranger.ActiveCodec.StorageSize / arranger.ArrangerPixelSize.Height;

                    newAddress += delta;
                    break;
                case ArrangerMoveType.RowUp:
                    if (arranger.Layout == ArrangerLayout.Tiled)
                        delta = arranger.ArrangerElementSize.Width * arranger.ActiveCodec.StorageSize;
                    else if (arranger.Layout == ArrangerLayout.Single)
                        delta = arranger.ActiveCodec.StorageSize / arranger.ArrangerPixelSize.Height;
                    newAddress -= delta;
                    break;
                case ArrangerMoveType.ColRight:
                    if (arranger.Layout == ArrangerLayout.Tiled)
                        delta = arranger.ActiveCodec.StorageSize;
                    else if (arranger.Layout == ArrangerLayout.Single)
                        delta = 16 * arranger.ActiveCodec.StorageSize / ((arranger.ActiveCodec.RowStride + arranger.ActiveCodec.Width) * arranger.ActiveCodec.Height);
                    newAddress += delta;
                    break;
                case ArrangerMoveType.ColLeft:
                    if (arranger.Layout == ArrangerLayout.Tiled)
                        delta = arranger.ActiveCodec.StorageSize;
                    else if (arranger.Layout == ArrangerLayout.Single)
                        delta = 16 * arranger.ActiveCodec.StorageSize / ((arranger.ActiveCodec.RowStride + arranger.ActiveCodec.Width) * arranger.ActiveCodec.Height);
                    newAddress -= delta;
                    break;
                case ArrangerMoveType.PageDown:
                    if (arranger.Layout == ArrangerLayout.Tiled)
                        delta = arranger.ArrangerElementSize.Width * arranger.ActiveCodec.StorageSize * arranger.ArrangerElementSize.Height / 2;
                    else if (arranger.Layout == ArrangerLayout.Single)
                        delta = arranger.ActiveCodec.StorageSize / 2;
                    newAddress += delta;
                    break;
                case ArrangerMoveType.PageUp:
                    if (arranger.Layout == ArrangerLayout.Tiled)
                        delta = arranger.ArrangerElementSize.Width * arranger.ActiveCodec.StorageSize * arranger.ArrangerElementSize.Height / 2;
                    else if (arranger.Layout == ArrangerLayout.Single)
                        delta = arranger.ActiveCodec.StorageSize / 2;
                    newAddress -= delta;
                    break;
                case ArrangerMoveType.Home:
                    newAddress = 0;
                    break;
                case ArrangerMoveType.End:
                    newAddress = new FileBitAddress(arranger.FileSize * 8 - arranger.ArrangerBitSize);
                    break;
            }

            if (newAddress + arranger.ArrangerBitSize > arranger.FileSize * 8) // Calculated address is past EOF (first)
                newAddress = new FileBitAddress(arranger.FileSize * 8 - arranger.ArrangerBitSize);

            if (newAddress < 0) // Calculated address is before start of file (second)
                newAddress = 0;

            if(initialAddress != newAddress)
                arranger.Move(newAddress);

            return newAddress;
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

        /// <summary>
        /// Find most frequent occurrence of an attribute within an arranger's elements
        /// </summary>
        /// <param name="arr">Arranger to search</param>
        /// <param name="attributeName">Name of the attribute to find most frequent value of</param>
        /// <returns></returns>
        public static string FindMostFrequentElementValue(this Arranger arr, string attributeName)
        {
            Type T = typeof(ArrangerElement);
            PropertyInfo P = T.GetProperty(attributeName);

            var query = from ArrangerElement el in arr.EnumerateElements()
                        group el by P.GetValue(el) into grp
                        select new { key = grp.Key, count = grp.Count() };

            return query.MaxBy(x => x.count).First().key as string;
        }
    }
}
