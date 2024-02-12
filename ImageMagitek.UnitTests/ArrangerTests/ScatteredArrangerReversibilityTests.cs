using ImageMagitek.Colors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ImageMagitek.UnitTests;
public partial class ScatteredArrangerReversibilityTests
{
    [Theory]
    [MemberData(nameof(ReverseCases))]
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
