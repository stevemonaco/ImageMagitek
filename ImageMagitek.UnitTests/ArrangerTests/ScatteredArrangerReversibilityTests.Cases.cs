using ImageMagitek.Colors;
using SixLabors.ImageSharp;
using Xunit;

namespace ImageMagitek.UnitTests;

public partial class ScatteredArrangerReversibilityTests
{
    public static TheoryData<string, ColorModel, bool, string, Size> ReverseCases => new()
    {
        { TestImages.Pattern1Bpp, ColorModel.Nes, false, "NES 1bpp", new Size(8, 8) },
        { TestImages.Bubbles, ColorModel.Bgr15, false, "NES 2bpp", new Size(8, 8) },
        { TestImages.EnsorcelledHiberation, ColorModel.Bgr15, false, "SNES 3bpp", new Size(8, 8) },
        { TestImages.Fireball, ColorModel.Bgr15, false, "SNES 4bpp", new Size(8, 8) },
        { TestImages.Ice, ColorModel.Bgr15, false, "Genesis 4bpp", new Size(8, 8) },
    };
}
