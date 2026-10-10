using System;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Image.Import;
using ImageMagitek.PluginSamples;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.ImageTests;

public sealed class ReadOnlyArrangerTests : IDisposable
{
    private static readonly Palette _palette = ArrangerTestFactory.CreatePalette(
        new ColorRgba32(0, 0, 0, 255), new ColorRgba32(255, 255, 255, 255));

    private readonly TempDataFiles _files = new();

    private static ScatteredArranger CreateReadOnlyArranger()
    {
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (_, _) => new IndexedCodecPluginAdapter(new LastArmageddonCodec(), _palette));
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
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (_, _) => new IndexedCodecPluginAdapter(new Psx8BppCodec(), _palette, 8, 8));

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

    [Fact]
    public void IsReadOnly_ElementOnReadOnlySource_IsTrue()
    {
        Assert.True(CreateReadOnlySourceArranger().IsReadOnly());
    }

    [Fact]
    public void GetReadOnlyReason_NamesCodecOrDataFile()
    {
        var codecArranger = CreateReadOnlyArranger();
        var codecName = codecArranger.GetElement(0, 0)!.Value.Codec.Name;
        var sourceArranger = CreateReadOnlySourceArranger();
        var writable = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (_, _) => new IndexedCodecPluginAdapter(new Psx8BppCodec(), _palette, 8, 8));

        Assert.Equal($"uses codec '{codecName}' that cannot encode", codecArranger.GetReadOnlyReason());
        Assert.Equal("reads data file 'readonly.bin', which is read-only", sourceArranger.GetReadOnlyReason());
        Assert.Null(writable.GetReadOnlyReason());
    }

    [Fact]
    public void SaveImage_ReadOnlySource_ThrowsBeforeWriting()
    {
        var arranger = CreateReadOnlySourceArranger();
        var source = arranger.GetElement(0, 0)!.Value.Source;
        var before = CodecTestHelpers.ReadAll(source);

        var image = new IndexedImage(arranger);
        Array.Fill(image.Image, (byte)1);

        var ex = Assert.Throws<InvalidOperationException>(image.SaveImage);
        Assert.Contains("readonly.bin", ex.Message);
        Assert.Equal(before, CodecTestHelpers.ReadAll(source));
    }

    [Fact]
    public void Prepare_ReadOnlySource_Fails()
    {
        var arranger = CreateReadOnlySourceArranger();
        var size = arranger.ArrangerPixelSize;
        var image = new DecodedImage(new ColorRgba32[size.Width * size.Height], size.Width, size.Height);

        var result = ImageImporter.Prepare(arranger, image, ImageImportOptions.Default);

        Assert.True(result.HasFailed);
        Assert.Contains("reads data file 'readonly.bin'", result.AsError.Reason);
    }

    private ScatteredArranger CreateReadOnlySourceArranger() =>
        ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (_, _) => new IndexedCodecPluginAdapter(new Psx8BppCodec(), _palette, 8, 8),
            _files.Open("readonly.bin", TestImageGenerator.RandomBytes(2 * 64, 13)));

    public void Dispose() => _files.Dispose();
}
