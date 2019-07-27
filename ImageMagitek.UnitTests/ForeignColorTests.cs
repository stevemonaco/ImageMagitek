using NUnit.Framework;
using ImageMagitek.Colors;

namespace ImageMagitek.UnitTests
{
    public class ForeignColorTests
    {
        [TestCaseSource(typeof(ForeignColorTestCases), "ToNativeTestCases")]
        public void ToNativeColor_Converts_Correctly(ForeignColor fc, NativeColor expected, ColorModel colorModel)
        {
            var actual = fc.ToNativeColor(colorModel);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(actual.Color, expected.Color, ".Color components not equal");
                Assert.AreEqual(actual.A, expected.A, "Alpha components not equal");
                Assert.AreEqual(actual.R, expected.R, "Red components not equal");
                Assert.AreEqual(actual.G, expected.G, "Green components not equal");
                Assert.AreEqual(actual.B, expected.B, "Blue components not equal");
            });
        }
    }
}
