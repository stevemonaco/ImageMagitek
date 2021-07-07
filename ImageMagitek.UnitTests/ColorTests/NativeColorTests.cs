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
            var colorFactory = new ColorFactory();
            var actual = colorFactory.ToForeign(nc, colorModel);

            if (actual is IColor32 actual32)
            {
                Assert.Multiple(() =>
                {
                    Assert.AreEqual(expected.Color, actual.Color, ".Color components not equal");
                    Assert.AreEqual(expected.R, actual32.R, "Red components not equal");
                    Assert.AreEqual(expected.G, actual32.G, "Green components not equal");
                    Assert.AreEqual(expected.B, actual32.B, "Blue components not equal");
                    Assert.AreEqual(expected.A, actual32.A, "Alpha components not equal");
                });
            }
        }
    }
}
