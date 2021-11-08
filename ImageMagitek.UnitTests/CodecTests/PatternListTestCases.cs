using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PC = ImageMagitek.Codec.PlaneCoordinate;

namespace ImageMagitek.Codec.UnitTests
{
    public class PatternListTestCases
    {
        public static IEnumerable<TestCaseData> TryCreateRemapPlanarPatternTestCases
        {
            get
            {
                yield return new TestCaseData
                (
                    new[] { "AAAAAAAACCCCCCCCBBBBBBBB" }, 24, 4, 1, 24,
                    new[]
                    {
                        new PC(0, 0, 0), new PC(1, 0, 0), new PC(2, 0, 0), new PC(3, 0, 0), new PC(4, 0, 0), new PC(5, 0, 0), new PC(6, 0, 0), new PC(7, 0, 0), new PC(16, 0, 0), new PC(17, 0, 0), new PC(18, 0, 0), new PC(19, 0, 0), new PC(20, 0, 0), new PC(21, 0, 0), new PC(22, 0, 0), new PC(23, 0, 0), new PC(8, 0, 0), new PC(9, 0, 0), new PC(10, 0, 0), new PC(11, 0, 0), new PC(12, 0, 0), new PC(13, 0, 0), new PC(14, 0, 0), new PC(15, 0, 0),
                        new PC(0, 1, 0), new PC(1, 1, 0), new PC(2, 1, 0), new PC(3, 1, 0), new PC(4, 1, 0), new PC(5, 1, 0), new PC(6, 1, 0), new PC(7, 1, 0), new PC(16, 1, 0), new PC(17, 1, 0), new PC(18, 1, 0), new PC(19, 1, 0), new PC(20, 1, 0), new PC(21, 1, 0), new PC(22, 1, 0), new PC(23, 1, 0), new PC(8, 1, 0), new PC(9, 1, 0), new PC(10, 1, 0), new PC(11, 1, 0), new PC(12, 1, 0), new PC(13, 1, 0), new PC(14, 1, 0), new PC(15, 1, 0),
                        new PC(0, 2, 0), new PC(1, 2, 0), new PC(2, 2, 0), new PC(3, 2, 0), new PC(4, 2, 0), new PC(5, 2, 0), new PC(6, 2, 0), new PC(7, 2, 0), new PC(16, 2, 0), new PC(17, 2, 0), new PC(18, 2, 0), new PC(19, 2, 0), new PC(20, 2, 0), new PC(21, 2, 0), new PC(22, 2, 0), new PC(23, 2, 0), new PC(8, 2, 0), new PC(9, 2, 0), new PC(10, 2, 0), new PC(11, 2, 0), new PC(12, 2, 0), new PC(13, 2, 0), new PC(14, 2, 0), new PC(15, 2, 0),
                        new PC(0, 3, 0), new PC(1, 3, 0), new PC(2, 3, 0), new PC(3, 3, 0), new PC(4, 3, 0), new PC(5, 3, 0), new PC(6, 3, 0), new PC(7, 3, 0), new PC(16, 3, 0), new PC(17, 3, 0), new PC(18, 3, 0), new PC(19, 3, 0), new PC(20, 3, 0), new PC(21, 3, 0), new PC(22, 3, 0), new PC(23, 3, 0), new PC(8, 3, 0), new PC(9, 3, 0), new PC(10, 3, 0), new PC(11, 3, 0), new PC(12, 3, 0), new PC(13, 3, 0), new PC(14, 3, 0), new PC(15, 3, 0),
                    },
                    new[]
                    {
                         0,  1,  2,  3,  4,  5,  6,  7, 16, 17, 18, 19, 20, 21, 22, 23,  8,  9, 10, 11, 12, 13, 14, 15,
                        24, 25, 26, 27, 28, 29, 30, 31, 40, 41, 42, 43, 44, 45, 46, 47, 32, 33, 34, 35, 36, 37, 38, 39,
                        48, 49, 50, 51, 52, 53, 54, 55, 64, 65, 66, 67, 68, 69, 70, 71, 56, 57, 58, 59, 60, 61, 62, 63,
                        72, 73, 74, 75, 76, 77, 78, 79, 88, 89, 90, 91, 92, 93, 94, 95, 80, 81, 82, 83, 84, 85, 86, 87
                    }
                );

                yield return new TestCaseData
                (
                    new[]
                    { 
                        "AAAABBBBDDDDCCCC",
                        "CCCCDDDDAAAABBBB"
                    }, 16, 4, 2, 32,
                    new[]
                    {
                        // Map from bit index 0...127
                        new PC(0, 0, 0), new PC(1, 0, 0), new PC(2, 0, 0), new PC(3, 0, 0), new PC(8, 0, 1), new PC(9, 0, 1), new PC(10, 0, 1), new PC(11, 0, 1), new PC(4, 0, 0), new PC(5, 0, 0), new PC(6, 0, 0), new PC(7, 0, 0), new PC(12, 0, 1), new PC(13, 0, 1), new PC(14, 0, 1), new PC(15, 0, 1),
                        new PC(12, 0, 0), new PC(13, 0, 0), new PC(14, 0, 0), new PC(15, 0, 0), new PC(0, 0, 1), new PC(1, 0, 1), new PC(2, 0, 1), new PC(3, 0, 1), new PC(8, 0, 0), new PC(9, 0, 0), new PC(10, 0, 0), new PC(11, 0, 0), new PC(4, 0, 1), new PC(5, 0, 1), new PC(6, 0, 1), new PC(7, 0, 1),

                        new PC(0, 1, 0), new PC(1, 1, 0), new PC(2, 1, 0), new PC(3, 1, 0), new PC(8, 1, 1), new PC(9, 1, 1), new PC(10, 1, 1), new PC(11, 1, 1), new PC(4, 1, 0), new PC(5, 1, 0), new PC(6, 1, 0), new PC(7, 1, 0), new PC(12, 1, 1), new PC(13, 1, 1), new PC(14, 1, 1), new PC(15, 1, 1),
                        new PC(12, 1, 0), new PC(13, 1, 0), new PC(14, 1, 0), new PC(15, 1, 0), new PC(0, 1, 1), new PC(1, 1, 1), new PC(2, 1, 1), new PC(3, 1, 1), new PC(8, 1, 0), new PC(9, 1, 0), new PC(10, 1, 0), new PC(11, 1, 0), new PC(4, 1, 1), new PC(5, 1, 1), new PC(6, 1, 1), new PC(7, 1, 1),

                        new PC(0, 2, 0), new PC(1, 2, 0), new PC(2, 2, 0), new PC(3, 2, 0), new PC(8, 2, 1), new PC(9, 2, 1), new PC(10, 2, 1), new PC(11, 2, 1), new PC(4, 2, 0), new PC(5, 2, 0), new PC(6, 2, 0), new PC(7, 2, 0), new PC(12, 2, 1), new PC(13, 2, 1), new PC(14, 2, 1), new PC(15, 2, 1),
                        new PC(12, 2, 0), new PC(13, 2, 0), new PC(14, 2, 0), new PC(15, 2, 0), new PC(0, 2, 1), new PC(1, 2, 1), new PC(2, 2, 1), new PC(3, 2, 1), new PC(8, 2, 0), new PC(9, 2, 0), new PC(10, 2, 0), new PC(11, 2, 0), new PC(4, 2, 1), new PC(5, 2, 1), new PC(6, 2, 1), new PC(7, 2, 1),

                        new PC(0, 3, 0), new PC(1, 3, 0), new PC(2, 3, 0), new PC(3, 3, 0), new PC(8, 3, 1), new PC(9, 3, 1), new PC(10, 3, 1), new PC(11, 3, 1), new PC(4, 3, 0), new PC(5, 3, 0), new PC(6, 3, 0), new PC(7, 3, 0), new PC(12, 3, 1), new PC(13, 3, 1), new PC(14, 3, 1), new PC(15, 3, 1),
                        new PC(12, 3, 0), new PC(13, 3, 0), new PC(14, 3, 0), new PC(15, 3, 0), new PC(0, 3, 1), new PC(1, 3, 1), new PC(2, 3, 1), new PC(3, 3, 1), new PC(8, 3, 0), new PC(9, 3, 0), new PC(10, 3, 0), new PC(11, 3, 0), new PC(4, 3, 1), new PC(5, 3, 1), new PC(6, 3, 1), new PC(7, 3, 1),
                    },
                    new[]
                    {
                        // Map from PlaneCoordinate(0, 0, 0) to PlaneCoordinate(15, 3, 1) in x, then y, then plane incrementing order
                        // Plane 0
                         0,   1,   2,   3,   8,   9,  10,  11,  24,  25,  26,  27,  16,  17,  18,  19,
                        32,  33,  34,  35,  40,  41,  42,  43,  56,  57,  58,  59,  48,  49,  50,  51,
                        64,  65,  66,  67,  72,  73,  74,  75,  88,  89,  90,  91,  80,  81,  82,  83,
                        96,  97,  98,  99, 104, 105, 106, 107, 120, 121, 122, 123, 112, 113, 114, 115,

                        // Plane 1
                         20,  21,  22,  23,  28,  29,  30,  31,   4,   5,   6,   7,  12,  13,  14,  15,
                         52,  53,  54,  55,  60,  61,  62,  63,  36,  37,  38,  39,  44,  45,  46,  47,
                         84,  85,  86,  87,  92,  93,  94,  95,  68,  69,  70,  71,  76,  77,  78,  79,
                        116, 117, 118, 119, 124, 125, 126, 127, 100, 101, 102, 103, 108, 109, 110, 111
                    }
                );

                yield return new TestCaseData
                (
                    new[]
                    {
                        "AABBCCDD",
                        "AABBCCDD",
                        "AABBCCDD",
                        "AABBCCDD"
                    }, 8, 2, 4, 32,
                    new[]
                    {
                        new PC(0, 0, 0), new PC(1, 0, 0), new PC(0, 0, 1), new PC(1, 0, 1), new PC(0, 0, 2), new PC(1, 0, 2), new PC(0, 0, 3), new PC(1, 0, 3), new PC(2, 0, 0), new PC(3, 0, 0), new PC(2, 0, 1), new PC(3, 0, 1), new PC(2, 0, 2), new PC(3, 0, 2), new PC(2, 0, 3), new PC(3, 0, 3),
                        new PC(4, 0, 0), new PC(5, 0, 0), new PC(4, 0, 1), new PC(5, 0, 1), new PC(4, 0, 2), new PC(5, 0, 2), new PC(4, 0, 3), new PC(5, 0, 3), new PC(6, 0, 0), new PC(7, 0, 0), new PC(6, 0, 1), new PC(7, 0, 1), new PC(6, 0, 2), new PC(7, 0, 2), new PC(6, 0, 3), new PC(7, 0, 3),

                        new PC(0, 1, 0), new PC(1, 1, 0), new PC(0, 1, 1), new PC(1, 1, 1), new PC(0, 1, 2), new PC(1, 1, 2), new PC(0, 1, 3), new PC(1, 1, 3), new PC(2, 1, 0), new PC(3, 1, 0), new PC(2, 1, 1), new PC(3, 1, 1), new PC(2, 1, 2), new PC(3, 1, 2), new PC(2, 1, 3), new PC(3, 1, 3),
                        new PC(4, 1, 0), new PC(5, 1, 0), new PC(4, 1, 1), new PC(5, 1, 1), new PC(4, 1, 2), new PC(5, 1, 2), new PC(4, 1, 3), new PC(5, 1, 3), new PC(6, 1, 0), new PC(7, 1, 0), new PC(6, 1, 1), new PC(7, 1, 1), new PC(6, 1, 2), new PC(7, 1, 2), new PC(6, 1, 3), new PC(7, 1, 3)
                    },
                    new[]
                    {
                        // Map from PlaneCoordinate(0, 0, 0) to PlaneCoordinate(7, 1, 3) in x, then y, then plane incrementing order
                        // Plane 0
                         0,  1,  8,  9, 16, 17, 24, 25,
                        32, 33, 40, 41, 48, 49, 56, 57,

                        // Plane 1
                         2,  3, 10, 11, 18, 19, 26, 27,
                        34, 35, 42, 43, 50, 51, 58, 59,

                        // Plane 2
                         4,  5, 12, 13, 20, 21, 28, 29,
                        36, 37, 44, 45, 52, 53, 60, 61,

                        // Plane 3
                         6,  7, 14, 15, 22, 23, 30, 31,
                        38, 39, 46, 47, 54, 55, 62, 63
                    }
                );
            }
        }

        public static IEnumerable<TestCaseData> TryCreateRemapChunkyPatternTestCases
        {
            get
            {
                yield return new TestCaseData
                (
                    new[]
                    {
                        "AABBCCDD",
                    }, 8, 2, 4, 32,
                    new[]
                    {
                        new PC(0, 0, 0), new PC(0, 0, 1), new PC(0, 0, 2), new PC(0, 0, 3), new PC(1, 0, 0), new PC(1, 0, 1), new PC(1, 0, 2), new PC(1, 0, 3), new PC(2, 0, 0), new PC(2, 0, 1), new PC(2, 0, 2), new PC(2, 0, 3), new PC(3, 0, 0), new PC(3, 0, 1), new PC(3, 0, 2), new PC(3, 0, 3), new PC(4, 0, 0), new PC(4, 0, 1), new PC(4, 0, 2), new PC(4, 0, 3), new PC(5, 0, 0), new PC(5, 0, 1), new PC(5, 0, 2), new PC(5, 0, 3), new PC(6, 0, 0), new PC(6, 0, 1), new PC(6, 0, 2), new PC(6, 0, 3), new PC(7, 0, 0), new PC(7, 0, 1), new PC(7, 0, 2), new PC(7, 0, 3),
                        new PC(0, 1, 0), new PC(0, 1, 1), new PC(0, 1, 2), new PC(0, 1, 3), new PC(1, 1, 0), new PC(1, 1, 1), new PC(1, 1, 2), new PC(1, 1, 3), new PC(2, 1, 0), new PC(2, 1, 1), new PC(2, 1, 2), new PC(2, 1, 3), new PC(3, 1, 0), new PC(3, 1, 1), new PC(3, 1, 2), new PC(3, 1, 3), new PC(4, 1, 0), new PC(4, 1, 1), new PC(4, 1, 2), new PC(4, 1, 3), new PC(5, 1, 0), new PC(5, 1, 1), new PC(5, 1, 2), new PC(5, 1, 3), new PC(6, 1, 0), new PC(6, 1, 1), new PC(6, 1, 2), new PC(6, 1, 3), new PC(7, 1, 0), new PC(7, 1, 1), new PC(7, 1, 2), new PC(7, 1, 3),

                    },
                    new[]
                    {
                        // Map from PlaneCoordinate(0, 0, 0) to PlaneCoordinate(7, 1, 3) in plane, then x, then y incrementing order
                        // Row 1
                         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31,

                         //Row 2
                        32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63
                    }
                );
            }
        }

        public static IEnumerable<TestCaseData> TryCreateRemapPatternTooManyLettersTestCases
        {
            get
            {
                yield return new TestCaseData
                (
                    new[] { "AAAAAAAACCCCCCCCCBBBBBBB" }, 24, 8, 1, 24
                );
            }
        }

        public static IEnumerable<TestCaseData> TryCreateRemapPatternOutOfRangeTestCases
        {
            get
            {
                yield return new TestCaseData
                (
                    new[] { "AAAAAAAACCCCCCCCCBBBBBBBB" }, 24, 8, 1, 23
                );

                yield return new TestCaseData
                (
                    new[] { "AAAAAAAACCCCCCCCCBBBBBBBBD" }, 24, 8, 1, 23
                );
            }
        }

        public static IEnumerable<TestCaseData> TryCreateRemapPatternInvalidSizeTestCases
        {
            get
            {
                yield return new TestCaseData
                (
                    new[] { "" }, 8, 8, 1, 0
                );

                yield return new TestCaseData
                (
                    new[] { "AAAAAAAA" }, 8, 8, 1, -1
                );
            }
        }

        public static IEnumerable<TestCaseData> TryCreateRemapPatternInvalidCharacterTestCases
        {
            get
            {
                yield return new TestCaseData
                (
                    new[] { "AAAAAAAACCCCCCCC00000000" }, 24, 8, 1, 24
                );

                yield return new TestCaseData
                (
                    new[] { "11111111CCCCCCCC,,,,,,,," }, 24, 8, 1, 24
                );
            }
        }
    }
}
