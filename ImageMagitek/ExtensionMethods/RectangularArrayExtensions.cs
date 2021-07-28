using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ImageMagitek.ExtensionMethods
{
    /// <summary>
    /// Extensions for 2D arrays
    /// </summary>
    public static class RectangularArrayExtensions
    {
        /// <summary>
        /// Casts a 2D array so that it can be used with Linq
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        /// <remarks>Credit to Jon Skeet's stackoverflow solution (https://stackoverflow.com/questions/27205568/c-sharp-linq-query-on-multidimensional-array)</remarks>
        public static IEnumerable<T> Cast<T>(this T[,] source)
        {
            foreach (T item in source)
                yield return item;
        }

        /// <summary>
        /// Creates a new subarray from the given source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Parent array to copy from in [y, x] ordering</param>
        /// <param name="x0">x-coordinate to start copying from</param>
        /// <param name="y0">y-coordinate to start copying from</param>
        /// <param name="width">Width of the copy</param>
        /// <param name="height">Height of the copy</param>
        /// <returns>The subarray in [y, x] ordering</returns>
        public static T[,] ToSubArray<T>(this T[,] source, int x0, int y0, int width, int height)
        {
            var subArray = new T[height, width];
            source.CopyToArray(subArray, x0, y0, width, height);
            return subArray;
        }

        /// <summary>
        /// Copies the given source array to the destination array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Source array to copy from in [y, x] ordering</param>
        /// <param name="dest">Destination array to copy to in [y, x] ordering</param>
        /// <param name="x0">x-coordinate of the source array to start copying from</param>
        /// <param name="y0">y-coordinate of the source array to start copying from</param>
        /// <param name="width">Width of the copy</param>
        /// <param name="height">Height of the copy</param>
        public static void CopyToArray<T>(this T[,] source, T[,] dest, int x0, int y0, int width, int height)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    dest[y, x] = source[y + y0, x + x0];
        }

        /// <summary>
        /// Copies a 1D array to a new 2D array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Source array to copy from</param>
        /// <param name="sourceX">x-coordinate of the array to start copying from</param>
        /// <param name="sourceY">y-coordinate of the array to start copying from</param>
        /// <param name="sourceWidth">Width of each row in source</param>
        /// <param name="width">Width of the copy</param>
        /// <param name="height">Height of the copy</param>
        /// <returns>The 2D array in [y, x] ordering</returns>
        public static T[,] To2DArray<T>(this T[] source, int sourceX, int sourceY, int sourceWidth, int width, int height)
        {
            var array = new T[height, width];
            source.CopyToArray2D(array, sourceX, sourceY, sourceWidth, width, height);
            return array;
        }

        /// <summary>
        /// Copies from a 1D array into an existing 2D array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Source array to copy from</param>
        /// <param name="dest">Destination array to copy into in [y, x] ordering</param>
        /// <param name="sourceX">x-coordinate of the source array to start copying from</param>
        /// <param name="sourceY">y-coordinate of the source array to start copying from</param>
        /// <param name="sourceWidth">Width of each row in source</param>
        /// <param name="destWidth">Width of the copy into dest</param>
        /// <param name="destHeight">Height of the copy into dest</param>
        public static void CopyToArray2D<T>(this T[] source, T[,] dest, int sourceX, int sourceY, int sourceWidth, int destWidth, int destHeight)
        {
            for (int y = 0; y < destHeight; y++)
                for (int x = 0; x < destWidth; x++)
                    dest[y, x] = source[(y + sourceY) * sourceWidth + x + sourceX];
        }

        /// <summary>
        /// Copies from a 1D array into an existing 2D array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Source array to copy from</param>
        /// <param name="sourceX">x-coordinate of the source array to start copying from</param>
        /// <param name="sourceY">y-coordinate of the source array to start copying from</param>
        /// <param name="sourceWidth"></param>
        /// <param name="dest">Destination array to copy into in [y, x] ordering</param>
        /// <param name="destX">x-coordinate of the dest array to start copying into</param>
        /// <param name="destY">y-coordinate of the dest array to start copying into</param>
        /// <param name="copyWidth">Width of the copy</param>
        /// <param name="copyHeight">Height of the copy</param>
        public static void CopyToArray2D<T>(this T[] source, int sourceX, int sourceY, int sourceWidth, T[,] dest, int destX, int destY, int copyWidth, int copyHeight)
        {
            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                {
                    dest[y + destY, x + destX] = source[(y + sourceY) * sourceWidth + x + sourceX];
                }
            }
        }

        /// <summary>
        /// Performs an in-place mirror of a 2D array's items
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Array to be mirrored</param>
        /// <param name="mirror">Mirror operation to apply</param>
        public static void MirrorArray2D<T>(this T[,] source, MirrorOperation mirror)
        {
            int width = source.GetLength(1);
            int height = source.GetLength(0);

            if (mirror == MirrorOperation.Horizontal && width > 1)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width / 2; x++)
                    {
                        SwapItem(source, x, y, width - 1 - x, y);
                    }
                }
            }
            else if (mirror == MirrorOperation.Vertical && height > 1)
            {
                for (int y = 0; y < height / 2; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        SwapItem(source, x, y, x, height - 1 - y);
                    }
                }
            }
            else if (mirror == MirrorOperation.Both && width > 1 && height > 1)
            {
                for (int y = 0; y < height / 2; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        SwapItem(source, x, y, width - 1 - x, height - 1 - y);
                    }
                }

                // Mirror center row horizontally for arrays with odd number of rows
                if (height % 2 == 1)
                {
                    int y = height / 2;
                    for (int x = 0; x < width / 2; x++)
                    {
                        SwapItem(source, x, y, width - 1 - x, y);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void SwapItem(T[,] source, int ax, int ay, int bx, int by)
            {
                T temp = source[ay, ax];
                source[ay, ax] = source[by, bx];
                source[by, bx] = temp;
            }
        }

        /// <summary>
        /// Peforms an in-place rotation of a 2D array's items
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Array to be rotated, must be a square array</param>
        /// <param name="rotation">Rotation operation to apply</param>
        /// <remarks>Implementation based on https://stackoverflow.com/questions/42519/how-do-you-rotate-a-two-dimensional-array </remarks>
        public static void RotateArray2D<T>(this T[,] source, RotationOperation rotation)
        {
            if (rotation == RotationOperation.None)
                return;

            int width = source.GetLength(1);
            int height = source.GetLength(0);

            if (width != height)
                throw new ArgumentException($"{nameof(RotateArray2D)} parameter '{nameof(source)}' must be a square array");

            if (width <= 1 || height <= 1)
                return;

            if (rotation == RotationOperation.Left)
            {
                source.MirrorArray2D(MirrorOperation.Horizontal);
                source.TransposeArray2D();
            }
            else if (rotation == RotationOperation.Right)
            {
                source.TransposeArray2D();
                source.MirrorArray2D(MirrorOperation.Horizontal);
            }
            else if (rotation == RotationOperation.Turn)
            {
                source.MirrorArray2D(MirrorOperation.Both);
            }
        }

        /// <summary>
        /// Performs an in-place transpose of a 2D square array's items
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">2D array to be transposed, must be square</param>
        public static void TransposeArray2D<T>(this T[,] source)
        {
            int width = source.GetLength(1);
            int height = source.GetLength(0);

            if (width != height)
                throw new ArgumentException($"{nameof(TransposeArray2D)} parameter '{nameof(source)}' must be a square array");

            if (width <= 1 || height <= 1)
                return;

            int len = source.GetLength(0);

            for (int i = 0; i < len; i++)
            {
                for (int j = i + 1; j < len; j++)
                {
                    var temp = source[i, j];
                    source[i, j] = source[j, i];
                    source[j, i] = temp;
                }
            }
        }
    }
}
