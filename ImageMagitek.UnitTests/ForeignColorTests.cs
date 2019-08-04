using NUnit.Framework;
using ImageMagitek.Colors;

namespace ImageMagitek.UnitTests
{
    public class ForeignColorTests
    {
        [TestCaseSource(typeof(ForeignColorTestCases), "ToNativeTestCases")]
        public void ToNative_AsExpected(IColor32 fc, ColorRgba32 expected)
        {
            var actual = ColorConverter.ToNative(fc);

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
