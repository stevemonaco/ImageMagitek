using ImageMagitek.ExtensionMethods;
using NUnit.Framework;
using System.Linq;

namespace ImageMagitek.UnitTests.ExtensionMethodTests;

public class RotateArray2DTests
{
    [TestCaseSource(typeof(RotateArray2DTestCases), "RotateLeftCases")]
    public void RotateArray2D_Left_AsExpected(byte[,] source, byte[,] expected)
    {
        source.RotateArray2D(RotationOperation.Left);
        CollectionAssert.AreEqual(source.Cast<byte>(), expected.Cast<byte>());
    }

    [TestCaseSource(typeof(RotateArray2DTestCases), "RotateRightCases")]
    public void RotateArray2D_Right_AsExpected(byte[,] source, byte[,] expected)
    {
        source.RotateArray2D(RotationOperation.Right);
        CollectionAssert.AreEqual(source.Cast<byte>(), expected.Cast<byte>());
    }

    [TestCaseSource(typeof(RotateArray2DTestCases), "RotateTurnCases")]
    public void RotateArray2D_Turn_AsExpected(byte[,] source, byte[,] expected)
    {
        source.RotateArray2D(RotationOperation.Turn);
        CollectionAssert.AreEqual(source.Cast<byte>(), expected.Cast<byte>());
    }
}
