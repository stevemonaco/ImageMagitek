using System.Linq;
using NUnit.Framework;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek.UnitTests.ExtensionMethodTests;

public class MirrorArray2DTests
{
    [TestCaseSource(typeof(MirrorArray2DTestCases), "MirrorHorizontalCases")]
    public void MirrorArray2D_HorizontalMirroring_AsExpected(byte[,] source, byte[,] expected)
    {
        source.MirrorArray2D(MirrorOperation.Horizontal);
        CollectionAssert.AreEqual(source.Cast<byte>(), expected.Cast<byte>());
    }

    [TestCaseSource(typeof(MirrorArray2DTestCases), "MirrorVerticalCases")]
    public void MirrorArray2D_VerticalMirroring_AsExpected(byte[,] source, byte[,] expected)
    {
        source.MirrorArray2D(MirrorOperation.Vertical);
        CollectionAssert.AreEqual(source.Cast<byte>(), expected.Cast<byte>());
    }

    [TestCaseSource(typeof(MirrorArray2DTestCases), "MirrorBothCases")]
    public void MirrorArray2D_BothMirroring_AsExpected(byte[,] source, byte[,] expected)
    {
        source.MirrorArray2D(MirrorOperation.Both);
        CollectionAssert.AreEqual(source.Cast<byte>(), expected.Cast<byte>());
    }
}
