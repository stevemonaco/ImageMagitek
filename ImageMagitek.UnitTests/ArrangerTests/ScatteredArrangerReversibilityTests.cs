using ImageMagitek.Colors;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek.UnitTests;

[TestFixture]
public class ScatteredArrangerReversibilityTests
{
    [TestCaseSource(typeof(ScatteredArrangerReversibilityTestCases), "ReverseCases")]
    public void ScatteredArranger_Reversibility_CanReverse(ScatteredArranger arranger, string imageFileName)
    {
        var exportedImageFileName = $"test.png";

        var indexedImage = new IndexedImage(arranger);
        indexedImage.ImportImage(imageFileName, new ImageSharpFileAdapter(), ColorMatchStrategy.Exact);
        indexedImage.ExportImage(exportedImageFileName, new ImageSharpFileAdapter());

        using var expected = Image<Rgba32>.Load<Rgba32>(imageFileName);
        using var actual = Image<Rgba32>.Load<Rgba32>(exportedImageFileName);

        ImageRgba32Assert.AreEqual(expected, actual);
    }
}
