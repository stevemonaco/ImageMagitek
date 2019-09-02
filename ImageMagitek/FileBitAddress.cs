using System;

namespace ImageMagitek
{
    /// <summary>
    /// Struct used to store a file address that does not start on a byte-aligned address
    /// </summary>
    public struct FileBitAddress : IEquatable<FileBitAddress>
    {
        /// <summary>
        /// File offset in bytes
        /// </summary>
        public long FileOffset { get; set; }

        /// <summary>
        /// Number of bits to skip after FileOffset
        /// Valid range is 0-7 inclusive
        /// A zero value would result in a byte-aligned address
        /// </summary>
        public int BitOffset { get; set; }

        public FileBitAddress(long fileOffset, int bitOffset)
        {
            if (bitOffset > 7)
                throw new ArgumentOutOfRangeException($"{nameof(FileBitAddress)}: {nameof(bitOffset)} {bitOffset} is out of range");

            FileOffset = fileOffset;
            BitOffset = bitOffset;
        }

        /// <summary>
        /// Construct a new FileBitAddress from the number of bits to the address
        /// </summary>
        /// <param name="bits"></param>
        public FileBitAddress(long bits)
        {
            FileOffset = bits / 8;
            BitOffset = (int)(bits % 8);
        }

        public long Bits()
        {
            return FileOffset * 8 + BitOffset;
        }

        public bool Equals(FileBitAddress other) =>
            FileOffset == other.FileOffset && BitOffset == other.BitOffset;

        public override bool Equals(object obj) =>
            Equals((FileBitAddress)obj);

        public static bool operator ==(FileBitAddress lhs, FileBitAddress rhs) =>
            lhs.Equals(rhs);

        public static bool operator !=(FileBitAddress lhs, FileBitAddress rhs) =>
            !lhs.Equals(rhs);

        /// <summary>
        /// Casts a long into a FileBitAddress
        /// </summary>
        /// <param name="Address">Number of bits</param>
        public static implicit operator FileBitAddress(long Address)
        {
            return new FileBitAddress(Address / 8, (int)(Address % 8));
        }

        /// <summary>
        /// Adds two FileBitAddress objects and returns the result
        /// </summary>
        /// <param name="Address1"></param>
        /// <param name="Address2"></param>
        /// <returns></returns>
        public static FileBitAddress operator +(FileBitAddress Address1, FileBitAddress Address2)
        {
            long bits = Address1.Bits() + Address2.Bits();
            return new FileBitAddress(bits);
        }

        /// <summary>
        /// Adds a specified number of bits to a FileBitAddress object
        /// </summary>
        /// <param name="Address"></param>
        /// <param name="Offset">Number of bits to advance the address</param>
        /// <returns></returns>
        public static FileBitAddress operator +(FileBitAddress Address, long Offset)
        {
            long bits = Address.Bits() + Offset;
            return new FileBitAddress(bits);
        }

        /// <summary>
        /// Subtracts two FileBitAddress objects and returns the result
        /// </summary>
        /// <param name="Address1"></param>
        /// <param name="Address2"></param>
        /// <returns></returns>
        public static FileBitAddress operator -(FileBitAddress Address1, FileBitAddress Address2)
        {
            long bits = Address1.Bits() - Address2.Bits();

            return new FileBitAddress(bits);
        }

        /// <summary>
        /// Subtracts a number of bits from a FileBitAddress object
        /// </summary>
        /// <param name="Address"></param>
        /// <param name="Offset"></param>
        /// <returns></returns>
        public static FileBitAddress operator -(FileBitAddress Address, long Offset)
        {
            long addressbits = Address.Bits();
            long retbits = addressbits - Offset;

            return new FileBitAddress(retbits);
        }

        public static bool operator <(FileBitAddress Address1, FileBitAddress Address2)
        {
            return Address1.Bits() < Address2.Bits();
        }

        public static bool operator <=(FileBitAddress Address1, FileBitAddress Address2)
        {
            return Address1.Bits() <= Address2.Bits();
        }

        public static bool operator >(FileBitAddress Address1, FileBitAddress Address2)
        {
            return Address1.Bits() > Address2.Bits();
        }
        public static bool operator >=(FileBitAddress Address1, FileBitAddress Address2)
        {
            return Address1.Bits() >= Address2.Bits();
        }
    }
}
