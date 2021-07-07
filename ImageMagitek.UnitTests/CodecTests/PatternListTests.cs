using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using NUnit.Framework;

namespace ImageMagitek.Codec.UnitTests
{
    [TestFixture]
    public class PatternListTests
    {
        [TestCaseSource(typeof(PatternListTestCases), "TryCreateRemapPlanarPatternTestCases")]
        public void TryCreateRemapPattern_DecodePlanar_AsExpected(IList<string> patterns, int width, int height,
            int planes, int size, IList<PlaneCoordinate> expectedDecoded, IList<int> expectedEncoded)
        {
            var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Planar, width, height, planes, size);

            result.Switch(
                success =>
                {
                    var actual = Enumerable.Range(0, width * height * planes)
                        .Select(x => success.Result.GetDecodeIndex(x))
                        .ToArray();

                    CollectionAssert.AreEqual(expectedDecoded, actual);
                },
                failed => Assert.Fail(failed.Reason));
        }

        [TestCaseSource(typeof(PatternListTestCases), "TryCreateRemapPlanarPatternTestCases")]
        public void TryCreateRemapPattern_EncodePlanar_AsExpected(IList<string> patterns, int width, int height,
            int planes, int size, IList<PlaneCoordinate> expectedDecoded, IList<int> expectedEncoded)
        {
            var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Planar, width, height, planes, size);

            result.Switch(
                success =>
                {
                    var actual = Enumerable.Range(0, planes)
                        .Cartesian(
                            Enumerable.Range(0, height),
                            Enumerable.Range(0, width),
                            (p, y, x) => new PlaneCoordinate((short)x, (short)y, (short)p))
                        .Select(x => success.Result.GetEncodeIndex(x))
                        .ToArray();

                    CollectionAssert.AreEqual(expectedEncoded, actual);
                },
                failed => Assert.Fail(failed.Reason));
        }

        [TestCaseSource(typeof(PatternListTestCases), "TryCreateRemapChunkyPatternTestCases")]
        public void TryCreateRemapPattern_DecodeChunky_AsExpected(IList<string> patterns, int width, int height,
            int planes, int size, IList<PlaneCoordinate> expectedDecoded, IList<int> expectedEncoded)
        {
            var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Chunky, width, height, planes, size);

            result.Switch(
                success =>
                {
                    var actual = Enumerable.Range(0, width * height * planes)
                        .Select(x => success.Result.GetDecodeIndex(x))
                        .ToArray();

                    CollectionAssert.AreEqual(expectedDecoded, actual);
                },
                failed => Assert.Fail(failed.Reason));
        }

        [TestCaseSource(typeof(PatternListTestCases), "TryCreateRemapChunkyPatternTestCases")]
        public void TryCreateRemapPattern_EncodeChunky_AsExpected(IList<string> patterns, int width, int height,
            int planes, int size, IList<PlaneCoordinate> expectedDecoded, IList<int> expectedEncoded)
        {
            var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Chunky, width, height, planes, size);

            result.Switch(
                success =>
                {
                    var actual = Enumerable.Range(0, height)
                        .Cartesian(
                            Enumerable.Range(0, width),
                            Enumerable.Range(0, planes),
                            (y, x, p) => new PlaneCoordinate((short)x, (short)y, (short)p))
                        .Select(x => success.Result.GetEncodeIndex(x))
                        .ToArray();

                    CollectionAssert.AreEqual(expectedEncoded, actual);
                },
                failed => Assert.Fail(failed.Reason));
        }

        [TestCaseSource(typeof(PatternListTestCases), "TryCreateRemapPatternTooManyLettersTestCases")]
        public void TryCreateRemapPattern_TooManyLetters_Fails(IList<string> patterns, int width, int height, int planes, int size)
        {
            var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Planar, width, height, planes, size);

            result.Switch(
                success => Assert.Fail("TryCreateRemapPattern should have failed, but succeeded"),
                failed => { });
        }

        [TestCaseSource(typeof(PatternListTestCases), "TryCreateRemapPatternOutOfRangeTestCases")]
        public void TryCreateRemapPattern_OutOfRange_Fails(IList<string> patterns, int width, int height, int planes, int size)
        {
            var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Planar, width, height, planes, size);

            result.Switch(
                success => Assert.Fail("TryCreateRemapPattern should have failed, but succeeded"),
                failed => { });
        }

        [TestCaseSource(typeof(PatternListTestCases), "TryCreateRemapPatternInvalidSizeTestCases")]
        public void TryCreateRemapPattern_InvalidSize_Fails(IList<string> patterns, int width, int height, int planes, int size)
        {
            var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Planar, width, height, planes, size);

            result.Switch(
                success => Assert.Fail("TryCreateRemapPattern should have failed, but succeeded"),
                failed => { });
        }

        [TestCaseSource(typeof(PatternListTestCases), "TryCreateRemapPatternInvalidCharacterTestCases")]
        public void TryCreateRemapPattern_InvalidCharacter_Fails(IList<string> patterns, int width, int height, int planes, int size)
        {
            var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Planar, width, height, planes, size);

            result.Switch(
                success => Assert.Fail("TryCreateRemapPattern should have failed, but succeeded"),
                failed => { });
        }
    }
}
