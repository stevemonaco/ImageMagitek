using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ImageMagitek.Codec.UnitTests
{
    [TestFixture]
    public class PatternListTests
    {
        [TestCaseSource(typeof(PatternListTestCases), "TryCreateRemapPatternTestCases")]
        public void TryCreateRemapPattern_AsExpected(IList<string> patterns, int size, IList<int> expected)
        {
            var result = PatternList.TryCreateRemapPattern(patterns, size);

            result.Switch(
                success => CollectionAssert.AreEqual(expected, success.Result),
                failed => Assert.Fail(failed.Reason));
        }

        [TestCaseSource(typeof(PatternListTestCases), "TryCreateRemapPatternTooManyLettersTestCases")]
        public void TryCreateRemapPattern_TooManyLetters_Fails(IList<string> patterns, int size)
        {
            var result = PatternList.TryCreateRemapPattern(patterns, size);

            result.Switch(
                success => Assert.Fail("TryCreateRemapPattern should have failed, but succeeded"),
                failed => { });
        }

        [TestCaseSource(typeof(PatternListTestCases), "TryCreateRemapPatternOutOfRangeTestCases")]
        public void TryCreateRemapPattern_OutOfRange_Fails(IList<string> patterns, int size)
        {
            var result = PatternList.TryCreateRemapPattern(patterns, size);

            result.Switch(
                success => Assert.Fail("TryCreateRemapPattern should have failed, but succeeded"),
                failed => { });
        }

        [TestCaseSource(typeof(PatternListTestCases), "TryCreateRemapPatternInvalidSizeTestCases")]
        public void TryCreateRemapPattern_InvalidSize_Fails(IList<string> patterns, int size)
        {
            var result = PatternList.TryCreateRemapPattern(patterns, size);

            result.Switch(
                success => Assert.Fail("TryCreateRemapPattern should have failed, but succeeded"),
                failed => { });
        }

        [TestCaseSource(typeof(PatternListTestCases), "TryCreateRemapPatternInvalidCharacterTestCases")]
        public void TryCreateRemapPattern_InvalidCharacter_Fails(IList<string> patterns, int size)
        {
            var result = PatternList.TryCreateRemapPattern(patterns, size);

            result.Switch(
                success => Assert.Fail("TryCreateRemapPattern should have failed, but succeeded"),
                failed => { });
        }
    }
}
