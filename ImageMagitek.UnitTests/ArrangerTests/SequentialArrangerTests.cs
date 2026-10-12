using System.Drawing;
using System.Linq;
using ImageMagitek.Builders;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.UnitTests.Fixtures;
using Xunit;

namespace ImageMagitek.UnitTests;

[Collection("Codec")]
public class SequentialArrangerTests
{
    private const string _codecName = "SNES 4bpp";

    private readonly CodecFixture _fixture;

    public SequentialArrangerTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Construct_ElementsUseGivenPalette()
    {
        var palette = TestImageGenerator.CreateDistinctPalette(4);

        var arranger = CreateArranger(palette);

        AssertElementsUse(arranger, palette);
    }

    [Fact]
    public void ArrangerBuilder_ElementsUsePalette()
    {
        var palette = TestImageGenerator.CreateDistinctPalette(4);

        SequentialArranger arranger = ArrangerBuilder
            .WithTiledLayout()
            .WithArrangerElementSize(4, 3)
            .WithElementPixelSize(8, 8)
            .WithPixelColorType(PixelColorType.Indexed)
            .WithName("test")
            .AsSequentialArranger(_fixture.CodecFactory)
            .WithDataFile(CreateSource())
            .WithCodecName(_codecName)
            .WithPalette(palette)
            .Build();

        Assert.Same(palette, arranger.ActivePalette);
        AssertElementsUse(arranger, palette);
    }

    [Fact]
    public void ChangeCodec_KeepsActivePalette()
    {
        var palette = TestImageGenerator.CreateDistinctPalette(4);
        var arranger = CreateArranger(palette);

        arranger.ChangeCodec(_fixture.CodecFactory.CreateCodec("SNES 2bpp")!);
        AssertElementsUse(arranger, palette);

        arranger.ChangeCodec(_fixture.CodecFactory.CreateCodec(_codecName, new Size(16, 16))!);
        Assert.Equal(new Size(16, 16), arranger.ElementPixelSize);
        AssertElementsUse(arranger, palette);
    }

    [Fact]
    public void Clone_KeepsActivePalette()
    {
        var palette = TestImageGenerator.CreateDistinctPalette(4);
        var arranger = CreateArranger(palette);

        var clone = arranger.CloneArranger();

        AssertElementsUse(clone, palette);
    }

    [Fact]
    public void Clone_MisalignedRectangle_CoversEveryTouchedElement()
    {
        var arranger = CreateArranger(TestImageGenerator.CreateDistinctPalette(4));

        var clone = arranger.CloneArranger(4, 0, 8, 8);

        Assert.Equal(new Size(2, 1), clone.ArrangerElementSize);
        Assert.Equal(arranger.GetElement(1, 0)!.Value.SourceAddress, clone.GetElement(1, 0)!.Value.SourceAddress);
    }

    [Fact]
    public void ChangePalette_AllElements()
    {
        var arranger = CreateArranger(TestImageGenerator.CreateDistinctPalette(4));
        var other = TestImageGenerator.CreateDistinctPalette(4);

        arranger.ChangePalette(other);

        Assert.Same(other, arranger.ActivePalette);
        AssertElementsUse(arranger, other);
    }

    [Fact]
    public void Move_LayoutNotStartingAtOrigin_PlacesFirstPatternCellAtAddress()
    {
        var arranger = CreateArranger(TestImageGenerator.CreateDistinctPalette(4));
        arranger.ChangeElementLayout(new TileLayout("Reversed", 2, 1, 2, [new Point(1, 0), new Point(0, 0)]));
        var storageSize = arranger.ActiveCodec.StorageSize;

        Assert.Equal(BitAddress.Zero, arranger.GetElement(1, 0)!.Value.SourceAddress);

        arranger.Move(new BitAddress(storageSize));

        Assert.Equal(new BitAddress(storageSize), arranger.GetElement(1, 0)!.Value.SourceAddress);
        Assert.Equal(new BitAddress(storageSize * 2), arranger.GetElement(0, 0)!.Value.SourceAddress);
    }

    private SequentialArranger CreateArranger(Palette palette)
    {
        var codec = _fixture.CodecFactory.CreateCodec(_codecName)!;
        return new SequentialArranger(4, 3, CreateSource(), palette, _fixture.CodecFactory, codec);
    }

    private static MemoryDataSource CreateSource() => new("test", 4096);

    private static void AssertElementsUse(Arranger arranger, Palette palette)
    {
        var elements = arranger.EnumerateElements().OfType<ArrangerElement>().ToList();

        Assert.NotEmpty(elements);
        Assert.All(elements, el => Assert.Same(palette, Assert.IsAssignableFrom<IIndexedCodec>(el.Codec).Palette));
    }
}
