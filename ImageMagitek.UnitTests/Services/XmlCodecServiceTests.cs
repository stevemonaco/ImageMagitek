using System;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Services;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.Services;

public class XmlCodecServiceTests
{
    [Fact]
    public void LoadCodecs_DuplicateName_KeepsFirstAndNamesBothFiles()
    {
        var directory = TestPaths.CreateTempPath("");
        Directory.CreateDirectory(directory);

        try
        {
            var shipped = Path.Combine(TestPaths.CodecsPath, "GBA4bpp.xml");
            var first = Path.Combine(directory, "a.xml");
            var second = Path.Combine(directory, "b.xml");
            File.Copy(shipped, first);
            File.WriteAllText(second, File.ReadAllText(shipped).Replace("<defaultwidth>8</defaultwidth>", "<defaultwidth>16</defaultwidth>"));

            var factory = new CodecFactory(ArrangerTestFactory.CreatePalette(), []);
            var service = new XmlCodecService(Path.Combine(AppContext.BaseDirectory, "_schemas", "CodecSchema.xsd"), factory);

            var result = service.LoadCodecs(directory);

            Assert.True(result.HasFailed);
            var reason = Assert.Single(result.AsError.Reasons);
            Assert.Contains(first, reason);
            Assert.Contains(second, reason);
            Assert.Equal(8, factory.CreateCodec("GBA 4bpp")!.Width);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
