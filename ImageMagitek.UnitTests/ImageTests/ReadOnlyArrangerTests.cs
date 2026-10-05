using System;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Image.Import;
using ImageMagitek.PluginSample;
using ImageMagitek.PluginSamples;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.ImageTests;

public class ReadOnlyArrangerTests
{
    private static readonly Palette _palette = ArrangerTestFactory.CreatePalette(
        new ColorRgba32(0, 0, 0, 255), new ColorRgba32(255, 255, 255, 255));

    private static ScatteredArranger CreateReadOnlyArranger()
    {
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (_, _) => new LastArmageddonCodec(_palette));
        var source = arranger.GetElement(0, 0)!.Value.Source;
        source.Write(BitAddress.Zero, TestImageGenerator.RandomBytes((int)source.Length, 11));
        return arranger;
    }

    [Fact]
    public void IsReadOnly_DecodeOnlyCodec_IsTrue()
    {
        Assert.True(CreateReadOnlyArranger().IsReadOnly());
    }

    [Fact]
    public void IsReadOnly_EncodableCodec_IsFalse()
    {
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (_, _) => new Psx8BppCodec(_palette, 8, 8));

        Assert.False(arranger.IsReadOnly());
    }

    [Fact]
    public void SaveImage_ReadOnly_ThrowsAndLeavesSourceUnchanged()
    {
        var arranger = CreateReadOnlyArranger();
        var source = arranger.GetElement(0, 0)!.Value.Source;
        var before = CodecTestHelpers.ReadAll(source);

        var image = new IndexedImage(arranger);
        Array.Fill(image.Image, (byte)1);

        Assert.Throws<InvalidOperationException>(image.SaveImage);
        Assert.Equal(before, CodecTestHelpers.ReadAll(source));
    }

    [Fact]
    public void Prepare_ReadOnly_Fails()
    {
        var arranger = CreateReadOnlyArranger();
        var size = arranger.ArrangerPixelSize;
        var image = new DecodedImage(new ColorRgba32[size.Width * size.Height], size.Width, size.Height);

        var result = ImageImporter.Prepare(arranger, image, ImageImportOptions.Default);

        Assert.True(result.HasFailed);
    }
}
