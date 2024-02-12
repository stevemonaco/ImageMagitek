using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using Xunit;

namespace ImageMagitek.Codec.UnitTests;
public partial class PatternListTests
{
    [Theory]
    [MemberData(nameof(TryCreateRemapPlanarPatternTestCases))]
    public void TryCreateRemapPattern_DecodePlanar_AsExpected(string[] patterns, int width, int height,
        int planes, int size, IList<PlaneCoordinate> expectedDecoded, int[] expectedEncoded)
    {
        var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Planar, width, height, planes, size);

        result.Switch(
            success =>
            {
                var actual = Enumerable.Range(0, width * height * planes)
                    .Select(x => success.Result.GetDecodeIndex(x))
                    .ToArray();

                Assert.Equal(expectedDecoded, actual);
            },
            failed => Assert.Fail(failed.Reason));
    }

    [Theory]
    [MemberData(nameof(TryCreateRemapPlanarPatternTestCases))]
    public void TryCreateRemapPattern_EncodePlanar_AsExpected(string[] patterns, int width, int height,
        int planes, int size, IList<PlaneCoordinate> expectedDecoded, int[] expectedEncoded)
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

                Assert.Equal(expectedEncoded, actual);
            },
            failed => Assert.Fail(failed.Reason));
    }

    [Theory]
    [MemberData(nameof(TryCreateRemapChunkyPatternTestCases))]
    public void TryCreateRemapPattern_DecodeChunky_AsExpected(string[] patterns, int width, int height,
        int planes, int size, IList<PlaneCoordinate> expectedDecoded, int[] expectedEncoded)
    {
        var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Chunky, width, height, planes, size);

        result.Switch(
            success =>
            {
                var actual = Enumerable.Range(0, width * height * planes)
                    .Select(x => success.Result.GetDecodeIndex(x))
                    .ToArray();

                Assert.Equal(expectedDecoded, actual);
            },
            failed => Assert.Fail(failed.Reason));
    }

    [Theory]
    [MemberData(nameof(TryCreateRemapChunkyPatternTestCases))]
    public void TryCreateRemapPattern_EncodeChunky_AsExpected(IList<string> patterns, int width, int height,
        int planes, int size, IList<PlaneCoordinate> expectedDecoded, int[] expectedEncoded)
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

                Assert.Equal(expectedEncoded, actual);
            },
            failed => Assert.Fail(failed.Reason));
    }

    [Theory]
    [MemberData(nameof(TryCreateRemapPatternTooManyLettersTestCases))]
    public void TryCreateRemapPattern_TooManyLetters_Fails(string[] patterns, int width, int height, int planes, int size)
    {
        var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Planar, width, height, planes, size);

        result.Switch(
            success => Assert.Fail("TryCreateRemapPattern should have failed, but succeeded"),
            failed => { });
    }

    [Theory]
    [MemberData(nameof(TryCreateRemapPatternOutOfRangeTestCases))]
    public void TryCreateRemapPattern_OutOfRange_Fails(string[] patterns, int width, int height, int planes, int size)
    {
        var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Planar, width, height, planes, size);

        result.Switch(
            success => Assert.Fail("TryCreateRemapPattern should have failed, but succeeded"),
            failed => { });
    }

    [Theory]
    [MemberData(nameof(TryCreateRemapPatternInvalidSizeTestCases))]
    public void TryCreateRemapPattern_InvalidSize_Fails(string[] patterns, int width, int height, int planes, int size)
    {
        var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Planar, width, height, planes, size);

        result.Switch(
            success => Assert.Fail("TryCreateRemapPattern should have failed, but succeeded"),
            failed => { });
    }

    [Theory]
    [MemberData(nameof(TryCreateRemapPatternInvalidCharacterTestCases))]
    public void TryCreateRemapPattern_InvalidCharacter_Fails(string[] patterns, int width, int height, int planes, int size)
    {
        var result = PatternList.TryCreatePatternList(patterns, PixelPacking.Planar, width, height, planes, size);

        result.Switch(
            success => Assert.Fail("TryCreateRemapPattern should have failed, but succeeded"),
            failed => { });
    }
}
