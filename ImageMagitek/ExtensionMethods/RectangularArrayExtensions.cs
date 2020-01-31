using System.Collections.Generic;

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

        public static T[,] ToSubArray<T>(this T[,] source, int x0, int y0, int width, int height)
        {
            var subArray = new T[width, height];
            source.CopyToArray(subArray, x0, y0, width, height);
            return subArray;
        }

        public static void CopyToArray<T>(this T[,] source, T[,] dest, int x0, int y0, int width, int height)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    dest[x, y] = source[x + x0, y + y0];
        }

        public static T[,] To2DArray<T>(this T[] source, int sourceX, int sourceY, int sourceWidth, int width, int height)
        {
            var array = new T[width, height];
            source.CopyToArray(array, sourceX, sourceY, sourceWidth, width, height);
            return array;
        }

        public static void CopyToArray<T>(this T[] source, T[,] dest, int sourceX, int sourceY, int sourceWidth, int destWidth, int destHeight)
        {
            for (int y = 0; y < destHeight; y++)
                for (int x = 0; x < destWidth; x++)
                    dest[x, y] = source[(y + sourceY) * sourceWidth + x + sourceX];
        }
    }
}
