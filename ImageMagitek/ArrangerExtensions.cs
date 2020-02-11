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
        /// <summary>
        /// Moves a Sequential Arranger's file position and updates each Element
        /// Will not move outside of the bounds of the underlying file
        /// </summary>
        /// <param name="moveType">Type of move requested</param>
        /// <returns>Updated address of first element</returns>
        public static FileBitAddress Move(this SequentialArranger self, ArrangerMoveType moveType)
        {
            if (self.Mode != ArrangerMode.Sequential)
                throw new InvalidOperationException($"{nameof(Move)}: Arranger {self.Name} is not in sequential mode");

            var initialAddress = self.GetInitialSequentialFileAddress();
            var newAddress = self.GetInitialSequentialFileAddress();
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
                    if(self.Layout == ArrangerLayout.Tiled)
                        delta = self.ArrangerElementSize.Width * self.ActiveCodec.StorageSize;
                    else if(self.Layout == ArrangerLayout.Single)
                        delta = self.ActiveCodec.StorageSize / self.ArrangerPixelSize.Height;

                    newAddress += delta;
                    break;
                case ArrangerMoveType.RowUp:
                    if (self.Layout == ArrangerLayout.Tiled)
                        delta = self.ArrangerElementSize.Width * self.ActiveCodec.StorageSize;
                    else if (self.Layout == ArrangerLayout.Single)
                        delta = self.ActiveCodec.StorageSize / self.ArrangerPixelSize.Height;
                    newAddress -= delta;
                    break;
                case ArrangerMoveType.ColRight:
                    if (self.Layout == ArrangerLayout.Tiled)
                        delta = self.ActiveCodec.StorageSize;
                    else if (self.Layout == ArrangerLayout.Single)
                        delta = 16 * self.ActiveCodec.StorageSize / ((self.ActiveCodec.RowStride + self.ActiveCodec.Width) * self.ActiveCodec.Height);
                    newAddress += delta;
                    break;
                case ArrangerMoveType.ColLeft:
                    if (self.Layout == ArrangerLayout.Tiled)
                        delta = self.ActiveCodec.StorageSize;
                    else if (self.Layout == ArrangerLayout.Single)
                        delta = 16 * self.ActiveCodec.StorageSize / ((self.ActiveCodec.RowStride + self.ActiveCodec.Width) * self.ActiveCodec.Height);
                    newAddress -= delta;
                    break;
                case ArrangerMoveType.PageDown:
                    if (self.Layout == ArrangerLayout.Tiled)
                        delta = self.ArrangerElementSize.Width * self.ActiveCodec.StorageSize * self.ArrangerElementSize.Height / 2;
                    else if (self.Layout == ArrangerLayout.Single)
                        delta = self.ActiveCodec.StorageSize / 2;
                    newAddress += delta;
                    break;
                case ArrangerMoveType.PageUp:
                    if (self.Layout == ArrangerLayout.Tiled)
                        delta = self.ArrangerElementSize.Width * self.ActiveCodec.StorageSize * self.ArrangerElementSize.Height / 2;
                    else if (self.Layout == ArrangerLayout.Single)
                        delta = self.ActiveCodec.StorageSize / 2;
                    newAddress -= delta;
                    break;
                case ArrangerMoveType.Home:
                    newAddress = 0;
                    break;
                case ArrangerMoveType.End:
                    newAddress = new FileBitAddress(self.FileSize * 8 - self.ArrangerBitSize);
                    break;
            }

            if (newAddress + self.ArrangerBitSize > self.FileSize * 8) // Calculated address is past EOF (first)
                newAddress = new FileBitAddress(self.FileSize * 8 - self.ArrangerBitSize);

            if (newAddress < 0) // Calculated address is before start of file (second)
                newAddress = 0;

            if(initialAddress != newAddress)
                self.Move(newAddress);

            return newAddress;
        }

        /// <summary>
        /// Moves the sequential arranger to the specified address
        /// If the arranger will overflow the file, then seek only to the furthest offset
        /// </summary>
        /// <param name="absoluteAddress">Specified address to move the arranger to</param>
        /// <returns></returns>
        public static FileBitAddress Move(this SequentialArranger arranger, FileBitAddress absoluteAddress)
        {
            if (arranger.Mode != ArrangerMode.Sequential)
                throw new InvalidOperationException($"{nameof(Move)}: Arranger {arranger.Name} is not in sequential mode");

            FileBitAddress address;
            FileBitAddress testaddress = absoluteAddress + arranger.ArrangerBitSize; // Tests the bounds of the arranger vs the file size

            if (arranger.FileSize * 8 < arranger.ArrangerBitSize) // Arranger needs more bits than the entire file
                address = new FileBitAddress(0, 0);
            else if (testaddress.Bits() > arranger.FileSize * 8)
                address = new FileBitAddress(arranger.FileSize * 8 - arranger.ArrangerBitSize);
            else
                address = absoluteAddress;

            int ElementStorageSize = arranger.ActiveCodec.StorageSize;

            for (int y = 0; y < arranger.ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < arranger.ArrangerElementSize.Width; x++)
                {
                    var el = arranger.GetElement(x, y);
                    el = el.WithAddress(address);
                    arranger.SetElement(el, x, y);
                    address += ElementStorageSize;
                }
            }

            return arranger.GetElement(0, 0).FileAddress;
        }

        /// <summary>
        /// Translates a point to an element location in the underlying arranger
        /// </summary>
        /// <param name="Location">Point in zoomed coordinates</param>
        /// <returns>Element location</returns>
        public static Point PointToElementLocation(this Arranger arranger, Point Location, int Zoom = 1)
        {
            Point unzoomed = new Point(Location.X / Zoom, Location.Y / Zoom);

            // Search list for element
            for (int y = 0; y < arranger.ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < arranger.ArrangerElementSize.Width; x++)
                {
                    ArrangerElement el = arranger.GetElement(x, y);
                    if (unzoomed.X >= el.X1 && unzoomed.X <= el.X2 && unzoomed.Y >= el.Y1 && unzoomed.Y <= el.Y2)
                        return new Point(x, y);
                }
            }

            throw new ArgumentOutOfRangeException($"{nameof(PointToElementLocation)} Location ({Location.X}, {Location.Y}) is out of range");
        }

        /// <summary>
        /// Find most frequent of an attribute within an arranger's elements
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
