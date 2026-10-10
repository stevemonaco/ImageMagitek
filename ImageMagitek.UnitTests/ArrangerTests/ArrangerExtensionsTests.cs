using System;
using System.IO;
using ImageMagitek.Colors;
using ImageMagitek.PluginSample;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.ArrangerTests;

public sealed class ArrangerExtensionsTests : IDisposable
{
    private readonly TempDataFiles _files = new();

    public void Dispose() => _files.Dispose();

    [Fact]
    public void FindMissingDataSource_ElementOrPaletteSource()
    {
        var elementSource = _files.Open("elements", new byte[128], isReadOnly: false);
        var paletteSource = _files.Open("palette", new byte[128], isReadOnly: false);
        var palette = new Palette("pal", new ColorFactory(), ColorModel.Rgba32,
            [new ProjectNativeColorSource(new ColorRgba32(0, 0, 0, 255))], false, PaletteStorageSource.ProjectXml, paletteSource);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (_, _) => new Psx8BppCodec(palette, 8, 8), elementSource);

        Assert.Null(arranger.FindMissingDataSource());

        File.Delete(paletteSource.FileLocation);
        Assert.Same(paletteSource, arranger.FindMissingDataSource());

        File.Delete(elementSource.FileLocation);
        Assert.Same(elementSource, arranger.FindMissingDataSource());
    }
}
