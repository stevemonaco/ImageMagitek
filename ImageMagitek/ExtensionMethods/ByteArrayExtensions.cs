using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.ExtensionMethods
{
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Implements an in-place left shift of array
        /// </summary>
        /// <param name="array">Array to shift</param>
        /// <param name="count">Number of times to left shift [0-7]</param>
        public static void ShiftLeft(this Span<byte> array, int count)
        {
            if (count < 0 || count > 7)
                throw new ArgumentOutOfRangeException($"{nameof(ShiftLeft)} parameter '{nameof(count)}' ({count}) is not within the valid range [0-7]");

            if (count == 0 || array.Length == 0)
                return;

            for (int i = 0; i < array.Length - 1; i++)
            {
                var left = array[i] << count;
                var right = array[i + 1] >> (8 - count);
                array[i] = (byte)(left | right);
            }

            array[array.Length - 1] <<= count;
        }

        /// <summary>
        /// Implements an in-place right shift of array
        /// </summary>
        /// <param name="array">Array to shift</param>
        /// <param name="count">Number of times to right shift [0-7]</param>
        public static void ShiftRight(this byte[] array, int count)
        {
            if (count < 0 || count > 7)
                throw new ArgumentOutOfRangeException($"{nameof(ShiftLeft)} parameter '{nameof(count)}' ({count}) is not within the valid range [0-7]");

            if (count == 0 || array.Length == 0)
                return;

            for (int i = array.Length - 1; i > 0; i--)
            {
                var left = array[i - 1] << (8 - count);
                var right = array[i] >> count;
                array[i] = (byte)(left | right);
            }

            array[0] >>= count;
        }
    }
}
