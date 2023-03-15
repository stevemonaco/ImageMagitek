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
            var codecService = new XmlCodecService(@"_schemas/CodecSchema.xsd");
            codecService.LoadCodecs(@"_codecs");
            var codecFactory = codecService.CodecFactory;

            string bubblesFontLocation = @"TestImages/2bpp/bubbles_font_2bpp.bmp";

            yield return new TestCaseData(
                ArrangerTestFactory.CreateIndexedArrangerFromImage(bubblesFontLocation,
                    Colors.ColorModel.Bgr15,
                    false,
                    codecFactory,
                    codecFactory.CreateCodec("NES 2bpp", new System.Drawing.Size(8, 8))!),
                bubblesFontLocation);

            yield return new TestCaseData(
                ArrangerTestFactory.CreateIndexedArrangerFromImage(bubblesFontLocation,
                    Colors.ColorModel.Bgr15,
                    false,
                    codecFactory,
                    codecFactory.CreateCodec("SNES 2bpp", new System.Drawing.Size(8, 8))!),
                bubblesFontLocation);
        }
    }
}
