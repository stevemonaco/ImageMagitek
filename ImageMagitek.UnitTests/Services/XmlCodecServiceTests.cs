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

            var failures = service.LoadCodecs(directory);

            var failure = Assert.Single(failures);
            Assert.Equal(second, failure.FileName);
            Assert.Contains(first, failure.Message);
            Assert.Equal(8, factory.CreateCodec("GBA 4bpp")!.Width);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void LoadCodecs_MalformedXml_ReportsAndLoadsTheRest()
    {
        var directory = TestPaths.CreateTempPath("");
        Directory.CreateDirectory(directory);

        try
        {
            var malformed = Path.Combine(directory, "a.xml");
            File.WriteAllText(malformed, "<flowcodec><name>Broken");
            File.Copy(Path.Combine(TestPaths.CodecsPath, "GBA4bpp.xml"), Path.Combine(directory, "b.xml"));

            var factory = new CodecFactory(ArrangerTestFactory.CreatePalette(), []);
            var service = new XmlCodecService(Path.Combine(AppContext.BaseDirectory, "_schemas", "CodecSchema.xsd"), factory);

            var failures = service.LoadCodecs(directory);

            var failure = Assert.Single(failures);
            Assert.Equal(malformed, failure.FileName);
            Assert.Contains("GBA 4bpp", factory.GetRegisteredCodecNames());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
