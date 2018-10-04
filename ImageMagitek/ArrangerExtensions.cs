using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using MoreLinq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMagitek
{
    /// <summary>
    /// Contains Arranger utility functions more relevant for GUI features
    /// </summary>
    public static class ArrangerExtensions
    {
        /// <summary>
        /// Gets a set of all distinct Palette keys used in an Arranger
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static HashSet<string> GetPaletteKeySet(this Arranger self)
        {
            HashSet<string> palSet = new HashSet<string>();

            for (int x = 0; x < self.ArrangerElementSize.Width; x++)
            {
                for (int y = 0; y < self.ArrangerElementSize.Height; y++)
                {
                    palSet.Add(self.ElementGrid[x, y].PaletteKey);
                }
            }

            return palSet;
        }

        /// <summary>
        /// Moves a Sequential Arranger's file position and updates each Element
        /// Will not move outside of the bounds of the underlying file
        /// </summary>
        /// <param name="moveType">Type of move requested</param>
        /// <returns>Updated address of first element</returns>
        public static FileBitAddress Move(this SequentialArranger self, ArrangerMoveType moveType)
        {
            if (self.Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException();

            if (self.ElementGrid is null)
                throw new NullReferenceException();

            FileBitAddress address = self.ElementGrid[0, 0].FileAddress;
            FileBitAddress delta;

            switch (moveType) // Calculate the new address based on the movement command. Negative and post-EOF addresses are handled after the switch
            {
                case ArrangerMoveType.ByteDown:
                    address += 8;
                    break;
                case ArrangerMoveType.ByteUp:
                    address -= 8;
                    break;
                case ArrangerMoveType.RowDown:
                    delta = self.ArrangerElementSize.Width * self.ElementGrid[0, 0].StorageSize;
                    address += delta;
                    break;
                case ArrangerMoveType.RowUp:
                    delta = self.ArrangerElementSize.Width * self.ElementGrid[0, 0].StorageSize;
                    address -= delta;
                    break;
                case ArrangerMoveType.ColRight:
                    delta = self.ElementGrid[0, 0].StorageSize;
                    address += delta;
                    break;
                case ArrangerMoveType.ColLeft:
                    delta = self.ElementGrid[0, 0].StorageSize;
                    address -= delta;
                    break;
                case ArrangerMoveType.PageDown:
                    delta = self.ArrangerElementSize.Width * self.ElementGrid[0, 0].StorageSize * self.ArrangerElementSize.Height / 2;
                    address += delta;
                    break;
                case ArrangerMoveType.PageUp:
                    delta = self.ArrangerElementSize.Width * self.ElementGrid[0, 0].StorageSize * self.ArrangerElementSize.Height / 2;
                    address -= delta;
                    break;
                case ArrangerMoveType.Home:
                    address = 0;
                    break;
                case ArrangerMoveType.End:
                    address = new FileBitAddress(self.FileSize * 8 - self.ArrangerBitSize);
                    break;
            }

            if (address + self.ArrangerBitSize > self.FileSize * 8) // Calculated address is past EOF (first)
                address = new FileBitAddress(self.FileSize * 8 - self.ArrangerBitSize);

            if (address < 0) // Calculated address is before start of file (second)
                address = 0;

            self.Move(address);

            return address;
        }

        /// <summary>
        /// Moves the sequential arranger to the specified address
        /// If the arranger will overflow the file, then seek only to the furthest offset
        /// </summary>
        /// <param name="absoluteAddress">Specified address to move the arranger to</param>
        /// <returns></returns>
        public static FileBitAddress Move(this SequentialArranger self, FileBitAddress absoluteAddress)
        {
            if (self.Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException();

            if (self.ElementGrid is null)
                throw new NullReferenceException();

            FileBitAddress address;
            FileBitAddress testaddress = absoluteAddress + self.ArrangerBitSize; // Tests the bounds of the arranger vs the file size

            if (self.FileSize * 8 < self.ArrangerBitSize) // Arranger needs more bits than the entire file
                address = new FileBitAddress(0, 0);
            else if (testaddress.Bits() > self.FileSize * 8)
                address = new FileBitAddress(self.FileSize * 8 - self.ArrangerBitSize);
            else
                address = absoluteAddress;

            int ElementStorageSize = self.ElementGrid[0, 0].StorageSize;

            for (int i = 0; i < self.ArrangerElementSize.Height; i++)
            {
                for (int j = 0; j < self.ArrangerElementSize.Width; j++)
                {
                    self.ElementGrid[j, i].FileAddress = address;
                    address += ElementStorageSize;
                }
            }

            return self.ElementGrid[0, 0].FileAddress;
        }

        /// <summary>
        /// Translates a point to an element location in the underlying arranger
        /// </summary>
        /// <param name="Location">Point in zoomed coordinates</param>
        /// <returns>Element location</returns>
        public static Point PointToElementLocation(this Arranger self, Point Location, int Zoom = 1)
        {
            Point unzoomed = new Point(Location.X / Zoom, Location.Y / Zoom);

            // Search list for element
            for (int y = 0; y < self.ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < self.ArrangerElementSize.Width; x++)
                {
                    ArrangerElement el = self.ElementGrid[x, y];
                    if (unzoomed.X >= el.X1 && unzoomed.X <= el.X2 && unzoomed.Y >= el.Y1 && unzoomed.Y <= el.Y2)
                        return new Point(x, y);
                }
            }

            throw new ArgumentOutOfRangeException("Location is outside of the range of all ArrangerElements in ElementList");
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

            var query = from ArrangerElement el in arr.ElementGrid
                        group el by P.GetValue(el) into grp
                        select new { key = grp.Key, count = grp.Count() };

            return query.MaxBy(x => x.count).First().key as string;
        }
    }
}
