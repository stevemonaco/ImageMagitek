using ImageMagitek.Codec;
using NUnit.Framework;

namespace ImageMagitek.UnitTests
{
    [TestFixture]
    public class ImagePropertyTests
    {
        [Test]
        public void ExtendRowPattern_AsExpected()
        {
            var expected = new int[] { 0, 1, 2, 3, 7, 6, 5, 4, 8, 9, 10, 11, 15, 14, 13, 12, 16, 17 };
            var ip = new ImageProperty(8, false, new int[] { 0, 1, 2, 3, 7, 6, 5, 4 });
            ip.ExtendRowPattern(18);

            CollectionAssert.AreEqual(expected, ip.RowExtendedPixelPattern);
        }
    }
}
