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
        public static int MaxPatternSize { get; } = 64 * 8;

        private const string _letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" + "abcdefghijklmnopqrstuvwxyz" + "23456789" + "!?@*";
        private readonly static Dictionary<char, int> _letterMapper = new Dictionary<char, int>
        (
            _letters.Select((x, i) => new KeyValuePair<char, int>(x, i))
        );

        public int PatternSize { get; }

        private readonly int[] _encodePattern;
        private readonly int[] _decodePattern;

        public PatternList(IEnumerable<int> remapPattern)
        {
            _decodePattern = remapPattern.ToArray();
            _encodePattern = remapPattern
                .Select((x, i) => new { Redirect = x, Index = i })
                .OrderBy(x => x.Redirect)
                .Select(x => x.Index)
                .ToArray();

            PatternSize = _decodePattern.Length;
        }

        public int GetDecodeIndex(int bitIndex)
        {
            if (bitIndex >= 0 && bitIndex < _encodePattern.Length)
                return _encodePattern[bitIndex];
            else
                throw new ArgumentOutOfRangeException($"{nameof(GetDecodeIndex)} argument {nameof(bitIndex)} is out of range");
        }

        public int GetEncodeIndex(int pixelIndex)
        {
            if (pixelIndex >= 0 && pixelIndex < _encodePattern.Length)
                return _encodePattern[pixelIndex];
            else
                throw new ArgumentOutOfRangeException($"{nameof(GetEncodeIndex)} argument {nameof(pixelIndex)} is out of range");
        }

        /// <summary>
        /// Tries to create an index remapper for the specified patterns
        /// </summary>
        /// <param name="pattern">List of pattern strings where each string contains the pattern for a single plane in first-to-last fill priority</param>
        /// <param name="size">Size in bits of all provided patterns combined</param>
        /// <returns>The index remapping pattern</returns>
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
