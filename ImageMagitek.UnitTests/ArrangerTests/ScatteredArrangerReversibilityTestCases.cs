using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using ImageMagitek.UnitTests.TestFactories;
using NUnit.Framework;
using System.Collections.Generic;

namespace ImageMagitek.UnitTests;

public class ScatteredArrangerReversibilityTestCases
{
    public static IEnumerable<TestCaseData> ReverseCases
    {
        get
        {
            var paletteService = new PaletteService(new ColorFactory());
            var palette = paletteService.ReadJsonPalette(@"_palettes/DefaultRgba32.json")!;
            var codecFactory = new CodecFactory(palette, new());
            var codecService = new XmlCodecService(@"_schemas/CodecSchema.xsd", codecFactory);
            codecService.LoadCodecs(@"_codecs");

            string bubblesFontLocation = @"TestImages/2bpp/bubbles_font_2bpp.bmp";

            yield return new TestCaseData(
                ArrangerTestFactory.CreateIndexedArrangerFromImage(bubblesFontLocation,
                    ColorModel.Bgr15,
                    false,
                    codecFactory,
                    codecFactory.CreateCodec("NES 2bpp", new System.Drawing.Size(8, 8))!),
                bubblesFontLocation);

            yield return new TestCaseData(
                ArrangerTestFactory.CreateIndexedArrangerFromImage(bubblesFontLocation,
                    ColorModel.Bgr15,
                    false,
                    codecFactory,
                    codecFactory.CreateCodec("SNES 2bpp", new System.Drawing.Size(8, 8))!),
                bubblesFontLocation);
        }
    }
}
