using NUnit.Framework;
using ImageMagitek.Colors;

namespace ImageMagitek.UnitTests
{
    [TestFixture]
    public class NativeColorTests
    {
        [TestCaseSource(typeof(NativeColorTestCases), "ToForeignTestCases")]
        public void ToForeignColor_Converts_Correctly(NativeColor nc, ForeignColor expected, ColorModel colorModel)
        {
            var actual = nc.ToForeignColor(colorModel);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(actual.Color, expected.Color, ".Color components not equal");
                Assert.AreEqual(actual.A(colorModel), expected.A(colorModel), "Alpha components not equal");
                Assert.AreEqual(actual.R(colorModel), expected.R(colorModel), "Red components not equal");
                Assert.AreEqual(actual.G(colorModel), expected.G(colorModel), "Green components not equal");
                Assert.AreEqual(actual.B(colorModel), expected.B(colorModel), "Blue components not equal");
            });
        }
    }
}
