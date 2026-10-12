using System;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using ImageMagitek.PluginSamples;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.ArrangerTests;

public class ElementTransformTests
{
    private const int Size = 8;

    private static readonly Palette _palette = ArrangerTestFactory.CreatePalette(Enumerable.Range(0, 16)
        .Select(i => new ColorRgba32((byte)(i * 8), (byte)(i * 4), (byte)(i * 2), 255))
        .ToArray());

    private static readonly MirrorOperation[] _mirrors = Enum.GetValues<MirrorOperation>();
    private static readonly RotationOperation[] _rotations = Enum.GetValues<RotationOperation>();

    public static TheoryData<MirrorOperation, RotationOperation, RotationOperation> RotateCases()
    {
        var data = new TheoryData<MirrorOperation, RotationOperation, RotationOperation>();
        foreach (var mirror in _mirrors)
            foreach (var rotation in _rotations)
                foreach (var rotate in new[] { RotationOperation.Left, RotationOperation.Right, RotationOperation.Turn })
                    data.Add(mirror, rotation, rotate);
        return data;
    }

    public static TheoryData<MirrorOperation, RotationOperation, MirrorOperation> MirrorCases()
    {
        var data = new TheoryData<MirrorOperation, RotationOperation, MirrorOperation>();
        foreach (var mirror in _mirrors)
            foreach (var rotation in _rotations)
                foreach (var flip in new[] { MirrorOperation.Horizontal, MirrorOperation.Vertical, MirrorOperation.Both })
                    data.Add(mirror, rotation, flip);
        return data;
    }

    private static ScatteredArranger CreateArranger(MirrorOperation mirror = MirrorOperation.None, RotationOperation rotation = RotationOperation.None)
    {
        var bytes = Enumerable.Range(0, Size * Size / 2).Select(i => (byte)(i * 37 + 11)).ToArray();
        var source = new MemoryDataSource("test", bytes.Length);
        source.Write(new BitAddress(0), bytes);

        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 1, 1,
            (_, _) => new IndexedCodecPluginAdapter(new Psx4BppCodec(), _palette, Size, Size), source);
        var element = arranger.GetElement(0, 0)!.Value.WithMirror(mirror).WithRotation(rotation);
        arranger.SetElement(element, 0, 0);
        return arranger;
    }

    private static byte[,] Render(Arranger arranger)
    {
        var image = new IndexedImage(arranger);
        var pixels = new byte[Size, Size];
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
                pixels[y, x] = image.GetPixel(x, y);
        return pixels;
    }

    [Theory]
    [MemberData(nameof(RotateCases))]
    public void Rotate_TurnsDisplayedPixels(MirrorOperation mirror, RotationOperation rotation, RotationOperation rotate)
    {
        var arranger = CreateArranger(mirror, rotation);
        var expected = Render(arranger);
        expected.RotateArray2D(rotate);

        Assert.True(arranger.TryRotateElement(0, 0, rotate).HasSucceeded);

        Assert.Equal(expected, Render(arranger));
    }

    [Theory]
    [MemberData(nameof(MirrorCases))]
    public void Mirror_FlipsDisplayedPixels(MirrorOperation mirror, RotationOperation rotation, MirrorOperation flip)
    {
        var arranger = CreateArranger(mirror, rotation);
        var expected = Render(arranger);
        expected.MirrorArray2D(flip);

        Assert.True(arranger.TryMirrorElement(0, 0, flip).HasSucceeded);

        Assert.Equal(expected, Render(arranger));
    }

    [Fact]
    public void HMirrorThenRotateLeft_TurnsCounterClockwise()
    {
        var arranger = CreateArranger();
        Assert.True(arranger.TryMirrorElement(0, 0, MirrorOperation.Horizontal).HasSucceeded);
        var before = Render(arranger);

        Assert.True(arranger.TryRotateElement(0, 0, RotationOperation.Left).HasSucceeded);

        var after = Render(arranger);
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
                Assert.Equal(before[x, Size - 1 - y], after[y, x]);
    }

    [Fact]
    public void StoredMeaning_RotateThenMirror()
    {
        var expected = Render(CreateArranger());
        expected.RotateArray2D(RotationOperation.Left);
        expected.MirrorArray2D(MirrorOperation.Horizontal);

        var actual = Render(CreateArranger(MirrorOperation.Horizontal, RotationOperation.Left));

        Assert.Equal(expected, actual);
    }
}
