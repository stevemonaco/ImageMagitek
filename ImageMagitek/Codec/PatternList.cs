using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageMagitek.Codec
{
    /// <summary>
    /// Provides index remapping with Feidian-style patterns
    /// </summary>
    /// <remarks>
    /// The pattern precedence is [A-Z] [a-z] [2-9] [!?@*] for a total of 64 characters
    /// </remarks>
    public class PatternList
    {
        private readonly static Dictionary<char, int> _letterMapper = new Dictionary<char, int>
        (
            ("ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
             "abcdefghijklmnopqrstuvwxyz" +
             "!?@*")
            .Select((x, i) => new KeyValuePair<char, int>(x, i))
        );

        public PatternList()
        {
            
        }

        /// <summary>
        /// Tries to create an index remapper for the specified patterns
        /// </summary>
        /// <param name="pattern">List of pattern strings where each string contains the pattern for a single plane in first-to-last fill priority</param>
        /// <param name="size">Size in bits of all provided patterns combined</param>
        /// <returns>The index remapping pattern</returns>
        /// <example>
        /// { "AAAAAAAACCCCCCCCBBBBBBBB" } -> 
        /// { 0, 1, 2, 3, 4, 5, 6, 7, 16, 17, 18, 19, 20, 21, 22, 23, 8, 9, 10, 11, 12, 13, 14, 15 }
        /// </example>
        public static MagitekResult<int[]> TryCreateRemapPattern(IList<string> patterns, int size)
        {
            if (size <= 0)
                return new MagitekResult<int[]>.Failed($"Pattern size ({size}) must be greater than 0");

            if (patterns?.Any() is false)
                throw new ArgumentException($"{nameof(TryCreateRemapPattern)} parameter '{nameof(patterns)}' must contain items");

            if (patterns.Any(x => string.IsNullOrWhiteSpace(x)))
                throw new ArgumentException($"{nameof(TryCreateRemapPattern)} parameter '{nameof(patterns)}' contains items that are null or empty");

            int patternLength = patterns.Sum(x => x.Length);

            if (patternLength != size)
                return new MagitekResult<int[]>.Failed($"The specified pattern size ({size}) did not match the size of the pattern sequences ({patternLength})");

            int plane = 1;
            int orderIndex = 0;
            var remapper = new int[size];
            var freqMap = new Dictionary<char, int>(_letterMapper.Keys.Select(x => new KeyValuePair<char, int>(x, 0)));

            foreach (var pattern in patterns)
            {
                foreach (char letter in pattern)
                {
                    if (_letterMapper.TryGetValue(letter, out var baseIndex))
                    {
                        var letterCount = freqMap[letter];

                        if (letterCount > 7)
                            return new MagitekResult<int[]>.Failed($"Letter '{letter}' occurs more than 8 times across all patterns");

                        var mapIndex = freqMap[letter] + baseIndex * 8;

                        if (mapIndex > size)
                            return new MagitekResult<int[]>.Failed($"Letter '{letter}' cannot be mapped to index '{mapIndex}' because the max index is '{size - 1}'");

                        remapper[orderIndex] = mapIndex;
                        freqMap[letter]++;
                        orderIndex++;
                    }
                    else
                        return new MagitekResult<int[]>.Failed($"Letter '{letter}' is not a valid pattern character");
                }

                plane++;
            }

            return new MagitekResult<int[]>.Success(remapper);
        }
    }
}
