using NUnit.Framework;
using ImageMagitek.Colors;

namespace ImageMagitek.UnitTests
{
    [TestFixture]
    public class NativeColorTests
    {
        [TestCaseSource(typeof(NativeColorTestCases), "ToForeignTestCases")]
        public void ToForeignColor_Converts_Correctly(ColorRgba32 nc, ColorBgr15 expected, ColorModel colorModel)
        {
            var actual = ColorConverter.ToForeign(nc, colorModel);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(expected.Color, actual.Color, ".Color components not equal");
                Assert.AreEqual(expected.R, actual.R, "Red components not equal");
                Assert.AreEqual(expected.G, actual.G, "Green components not equal");
                Assert.AreEqual(expected.B, actual.B, "Blue components not equal");
                Assert.AreEqual(expected.A, actual.A, "Alpha components not equal");
            });
        }
    }
}
