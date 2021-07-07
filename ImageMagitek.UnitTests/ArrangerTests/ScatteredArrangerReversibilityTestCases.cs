using ImageMagitek.Services;
using ImageMagitek.UnitTests.TestFactories;
using NUnit.Framework;
using System.Collections.Generic;

namespace ImageMagitek.UnitTests
{
    public class ScatteredArrangerReversibilityTestCases
    {
        public static IEnumerable<TestCaseData> ReverseCases
        {
            get
            {
                var codecService = new CodecService(@"_schemas/CodecSchema.xsd");
                codecService.LoadXmlCodecs(@"_codecs");
                var codecFactory = codecService.CodecFactory;

                yield return new TestCaseData(
                    ArrangerTestFactory.CreateIndexedArrangerFromImage(@"TestImages/bubbles_font_2bpp.bmp",
                        Colors.ColorModel.Bgr15,
                        false,
                        codecFactory,
                        codecFactory.GetCodec("NES 2bpp", new System.Drawing.Size(8, 8))),
                    @"TestImages/bubbles_font_2bpp.bmp");

                yield return new TestCaseData(
                    ArrangerTestFactory.CreateIndexedArrangerFromImage(@"TestImages/bubbles_font_2bpp.bmp",
                        Colors.ColorModel.Bgr15,
                        false,
                        codecFactory,
                        codecFactory.GetCodec("SNES 2bpp", new System.Drawing.Size(8, 8))),
                    @"TestImages/bubbles_font_2bpp.bmp");
            }
        }
    }
}
