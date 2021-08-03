using MoreLinq;
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
    public sealed class PatternList
    {
        public static int MaxPatternSize { get; } = 64 * 8;

        private const string _letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" + "abcdefghijklmnopqrstuvwxyz" + "23456789" + "!?@*";
        private readonly static Dictionary<char, int> _letterMapper = new Dictionary<char, int>
        (
            _letters.Select((x, i) => new KeyValuePair<char, int>(x, i))
        );

        public int PatternSize { get; }
        public int ImageSize { get; set; }
        public int Width { get; }
        public int Height { get; }
        public int Planes { get; }

        private readonly PlaneCoordinate[] _decodePattern;
        public Dictionary<PlaneCoordinate, int> _encodePattern;

        private PatternList(PlaneCoordinate[] decodePattern, Dictionary<PlaneCoordinate, int> encodePattern, int width, int height, int planes)
        {
            Width = width;
            Height = height;
            Planes = planes;
            ImageSize = Width * Height * Planes;

            _decodePattern = decodePattern;
            _encodePattern = encodePattern;
        }

        public PlaneCoordinate GetDecodeIndex(int bitIndex)
        {
            if (bitIndex >= 0 && bitIndex < ImageSize)
                return _decodePattern[bitIndex]; // % PatternSize];
            else
                throw new ArgumentOutOfRangeException($"{nameof(GetDecodeIndex)} argument {nameof(bitIndex)} is out of range");
        }

        public int GetEncodeIndex(PlaneCoordinate coordinate)
        {
            if (_encodePattern.TryGetValue(coordinate, out var bitIndex))
                return bitIndex;
            throw new KeyNotFoundException($"{nameof(GetEncodeIndex)} argument {nameof(coordinate)} ({coordinate.X}, {coordinate.Y}, {coordinate.P}) was found in the encoder remapping");
        }

        /// <summary>
        /// Tries to create a PatternList
        /// </summary>
        /// <param name="patterns">List of pattern strings containing only valid pattern characters</param>
        /// <param name="width">Width of image in pixels</param>
        /// <param name="height">Height of image in pixels</param>
        /// <param name="planes">Number of planes in the image</param>
        /// <param name="patternSize">Combined size of all patterns in number of characters (bits)</param>
        /// <returns></returns>
        public static MagitekResult<PatternList> TryCreatePatternList(IList<string> patterns, PixelPacking packing, int width, int height, int planes, int patternSize)
        {
            if (patternSize <= 0)
                return new MagitekResult<PatternList>.Failed($"Pattern size ({patternSize}) must be greater than 0");

            if (patterns?.Any() is false)
                throw new ArgumentException($"{nameof(TryCreatePatternList)} parameter '{nameof(patterns)}' must contain items");

            if (patterns.Any(x => string.IsNullOrWhiteSpace(x)))
                throw new ArgumentException($"{nameof(TryCreatePatternList)} parameter '{nameof(patterns)}' contains items that are null or empty");

            int patternsLengthSum = default;
            if (packing == PixelPacking.Planar)
                patternsLengthSum = patterns.Sum(x => x.Length);
            else
                patternsLengthSum = patterns.First().Length * planes;

            if (patternSize != patternsLengthSum)
                return new MagitekResult<PatternList>.Failed($"The specified pattern size ({patternSize}) did not match the size of the pattern sequences ({patternsLengthSum})");

            if (packing == PixelPacking.Planar && patterns.Count != planes)
                throw new ArgumentException($"{nameof(PixelPacking)}.{PixelPacking.Planar} must contain the same number of patterns as the color depth");

            if (packing == PixelPacking.Chunky && patterns.Count != 1)
                return new MagitekResult<PatternList>.Failed($"{nameof(PixelPacking)}.{PixelPacking.Chunky} must contain only one pattern");

            var imageSize = width * height * planes;
            if (imageSize % patternSize != 0)
                return new MagitekResult<PatternList>.Failed($"The image size ({imageSize}) is not an even multiple of the pattern size ({patternSize})");

            if (patterns.Any(x => x.Length != patterns[0].Length))
                return new MagitekResult<PatternList>.Failed($"The pattern length of all patterns must be equal");

            if (packing == PixelPacking.Planar)
                return TryCreatePlanarPatternList(patterns, width, height, planes, patternSize);
            else if (packing == PixelPacking.Chunky)
                return TryCreateChunkyPatternList(patterns, width, height, planes, patternSize);
            else
                throw new NotSupportedException($"{nameof(TryCreatePatternList)} does not support {nameof(PixelPacking)} of value {packing}");
        }

        private static MagitekResult<PatternList> TryCreatePlanarPatternList(IList<string> patterns,
            int width, int height, int planes, int patternSize)
        {
            var freqMap = new Dictionary<char, int>(_letterMapper.Keys.Select(x => new KeyValuePair<char, int>(x, 0)));
            int planeSize = height * width;
            var imageSize = width * height * planes;
            int planePatternSize = patternSize / planes;

            var decodePattern = Enumerable.Repeat(new PlaneCoordinate(0, 0, 0), imageSize).ToArray();
            short plane = 0;

            foreach (var pattern in patterns)
            {
                var planeDecodePattern = new int[pattern.Length];

                var pixel = 0;

                // Map the pattern-defined portion of the plane
                foreach (char letter in pattern)
                {
                    if (!_letterMapper.TryGetValue(letter, out var baseIndex))
                        return new MagitekResult<PatternList>.Failed($"Letter '{letter}' is not a valid pattern character");

                    var letterCount = freqMap[letter];

                    if (letterCount > 7)
                        return new MagitekResult<PatternList>.Failed($"Letter '{letter}' occurs more than 8 times across all patterns");

                    var mapIndex = freqMap[letter] + baseIndex * 8;
                    freqMap[letter]++;

                    if (mapIndex > patternSize)
                        return new MagitekResult<PatternList>.Failed($"Letter '{letter}' cannot be mapped to index '{mapIndex}' because the max index is '{patternSize - 1}'");

                    planeDecodePattern[pixel] = mapIndex;
                    pixel++;
                }

                // Extend pattern to fill entire plane
                var extendedPattern = ExtendPattern(planeDecodePattern, width, height, plane, planePatternSize, patternSize);
                foreach (var item in extendedPattern)
                    decodePattern[item.MapIndex] = item.Coordinate;

                plane++;
            }

            var encodePattern = decodePattern.Select((x, i) => new { Coordinate = x, Index = i })
                .ToDictionary(x => x.Coordinate, x => x.Index);

            var patternList = new PatternList(decodePattern, encodePattern, width, height, planes);
            return new MagitekResult<PatternList>.Success(patternList);

            static IEnumerable<(PlaneCoordinate Coordinate, int MapIndex)> ExtendPattern
                (IList<int> decodePattern, int width, int height, short plane, int planePatternSize, int patternSize)
            {
                int imageSize = width * height;
                int pixelIndex = 0;
                int repeat = 0;

                while (pixelIndex < width * height)
                {
                    int extendSize = Math.Min(planePatternSize, imageSize);
                    for (int i = 0; i < extendSize; i++)
                    {
                        var bitIndex = decodePattern[pixelIndex % planePatternSize];
                        short x = (short)(pixelIndex % width);
                        short y = (short)(pixelIndex / width);
                        var coord = new PlaneCoordinate(x, y, plane);
                        var index = bitIndex + repeat * patternSize;

                        yield return (coord, index);
                        pixelIndex++;
                    }

                    repeat++;
                }
            }
        }

        private static MagitekResult<PatternList> TryCreateChunkyPatternList(IList<string> patterns,
            int width, int height, int planes, int patternSize)
        {
            var freqMap = new Dictionary<char, int>(_letterMapper.Keys.Select(x => new KeyValuePair<char, int>(x, 0)));
            int planeSize = height * width;
            var imageSize = width * height * planes;
            int planePatternSize = patternSize / planes;

            var imageDecodePattern = Enumerable.Repeat(new PlaneCoordinate(0, 0, 0), imageSize).ToArray();

            var maxInstancesPerCharacter = planes switch
            {
                1 => 8,
                2 => 4,
                3 => 2,
                4 => 2,
                5 => 1,
                6 => 1,
                7 => 1,
                8 => 1,
                _ => throw new ArgumentOutOfRangeException($"{nameof(TryCreateChunkyPatternList)} parameter {nameof(planes)} ({planes}) is out of range")
            };

            var pattern = patterns.First();
            var decodePattern = new int[pattern.Length * planes];

            var pixel = 0;

            // Map the pattern-defined portion of the plane
            foreach (char letter in pattern)
            {
                if (!_letterMapper.TryGetValue(letter, out var baseIndex))
                    return new MagitekResult<PatternList>.Failed($"Letter '{letter}' is not a valid pattern character");

                var letterCount = freqMap[letter];

                if (letterCount > maxInstancesPerCharacter)
                    return new MagitekResult<PatternList>.Failed($"Letter '{letter}' occurs more than {maxInstancesPerCharacter} times across the pattern");

                for (int i = 0; i < planes; i++)
                {
                    var mapIndex = freqMap[letter] * planes + i + baseIndex * maxInstancesPerCharacter * planes;

                    if (mapIndex > patternSize)
                        return new MagitekResult<PatternList>.Failed($"Letter '{letter}' cannot be mapped to index '{mapIndex}' because the max index is '{patternSize - 1}'");

                    decodePattern[pixel] = mapIndex;
                    pixel++;
                }
                freqMap[letter]++;
            }

            // Extend pattern to fill entire image
            var extendedPattern = ExtendChunkyPattern(decodePattern, width, height, planes, patternSize);
            foreach (var item in extendedPattern)
                imageDecodePattern[item.MapIndex] = item.Coordinate;

            var encodePattern = imageDecodePattern.Select((x, i) => new { Coordinate = x, Index = i })
                .ToDictionary(x => x.Coordinate, x => x.Index);

            var patternList = new PatternList(imageDecodePattern, encodePattern, width, height, planes);
            return new MagitekResult<PatternList>.Success(patternList);

            static IEnumerable<(PlaneCoordinate Coordinate, int MapIndex)> ExtendChunkyPattern
                (IList<int> decodePattern, int width, int height, int bitsPerPixel, int patternSize)
            {
                int imageSize = width * height * bitsPerPixel;
                int pixelIndex = 0;
                int repeat = 0;

                while (pixelIndex < width * height * bitsPerPixel)
                {
                    int extendSize = Math.Min(patternSize, imageSize);
                    for (int i = 0; i < extendSize; i++)
                    {
                        var bitIndex = decodePattern[pixelIndex % patternSize];
                        short x = (short)((pixelIndex / bitsPerPixel) % width);
                        short y = (short)((pixelIndex / bitsPerPixel) / width);
                        short p = (short)(pixelIndex % bitsPerPixel);
                        var coord = new PlaneCoordinate(x, y, p);
                        var index = bitIndex + repeat * patternSize;

                        yield return (coord, index);
                        pixelIndex++;
                    }

                    repeat++;
                }
            }
        }
    }
}
