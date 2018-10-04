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
    }
}
