using System.Collections.Generic;
using NUnit.Framework;

namespace ImageMagitek.Codec.UnitTests
{
    public class PatternListTestCases
    {
        public static IEnumerable<TestCaseData> TryCreateRemapPatternTestCases
        {
            get
            {
                yield return new TestCaseData
                (
                    new[] { "AAAAAAAACCCCCCCCBBBBBBBB" }, 24,
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 16, 17, 18, 19, 20, 21, 22, 23, 8, 9, 10, 11, 12, 13, 14, 15 }
                );

                yield return new TestCaseData
                (
                    new[] 
                    { 
                        "AAAABBBBDDDDCCCC",
                        "CCCCDDDDAAAABBBB"
                    }, 32,
                    new[] 
                    {
                         0,   1,  2,  3,  8,  9, 10, 11, 24, 25, 26, 27, 16, 17, 18, 19,
                         20, 21, 22, 23, 28, 29, 30, 31,  4,  5,  6,  7, 12, 13, 14, 15
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
                    new[] { "AAAAAAAACCCCCCCCCBBBBBBB" }, 24
                );
            }
        }

        public static IEnumerable<TestCaseData> TryCreateRemapPatternOutOfRangeTestCases
        {
            get
            {
                yield return new TestCaseData
                (
                    new[] { "AAAAAAAACCCCCCCCCBBBBBBBB" }, 23
                );

                yield return new TestCaseData
                (
                    new[] { "AAAAAAAACCCCCCCCCBBBBBBBBD" }, 24
                );
            }
        }

        public static IEnumerable<TestCaseData> TryCreateRemapPatternInvalidSizeTestCases
        {
            get
            {
                yield return new TestCaseData
                (
                    new[] { "" }, 0
                );

                yield return new TestCaseData
                (
                    new[] { "AAAAAAAA" }, -1
                );
            }
        }

        public static IEnumerable<TestCaseData> TryCreateRemapPatternInvalidCharacterTestCases
        {
            get
            {
                yield return new TestCaseData
                (
                    new[] { "AAAAAAAACCCCCCCC00000000" }, 24
                );

                yield return new TestCaseData
                (
                    new[] { "11111111CCCCCCCC,,,,,,,," }, 24
                );
            }
        }
    }
}
