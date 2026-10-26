using System;
using System.Drawing;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using ImageMagitek.PluginSamples;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.ImageTests;

public sealed class ArrangerViewTests : IDisposable
{
    private static readonly Palette _palette = ArrangerTestFactory.CreatePalette(Enumerable.Range(0, 16)
        .Select(i => new ColorRgba32((byte)(i * 16), (byte)(255 - i * 16), (byte)i, 255)).ToArray());

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"view-{Guid.NewGuid():N}.png");

    public void Dispose() => File.Delete(_path);

    /// <summary>
    /// A view that is not an arranger: it has no mutators, only the elements it was given
    /// </summary>
    private sealed class GridView(ArrangerElement?[,] elements, Size elementSize) : IArrangerView
    {
        public string Name => "view";
        public Size ArrangerElementSize => new(elements.GetLength(1), elements.GetLength(0));
        public Size ElementPixelSize => elementSize;
        public Size ArrangerPixelSize => new(ArrangerElementSize.Width * elementSize.Width, ArrangerElementSize.Height * elementSize.Height);
        public ElementLayout Layout => ElementLayout.Tiled;
        public PixelColorType ColorType => PixelColorType.Indexed;
        public ArrangerElement? GetElement(int elemX, int elemY) => elements[elemY, elemX];
    }

    private static (ScatteredArranger Arranger, GridView View) CreatePair()
    {
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 3, 2,
            (_, _) => new IndexedCodecPluginAdapter(new Psx4BppCodec(), _palette, 8, 8));
        var source = arranger.GetElement(0, 0)!.Value.Source;
        source.Write(BitAddress.Zero, TestImageGenerator.RandomBytes((int)source.Length, 5));
        arranger.ResetElement(1, 1);
        arranger.SetElement(arranger.GetElement(0, 0)!.Value.WithMirror(MirrorOperation.Horizontal), 2, 1);

        var elements = new ArrangerElement?[2, 3];
        foreach (var (x, y) in arranger.EnumerateElementsWithinElementRange())
            elements[y, x] = arranger.GetElement(x, y);

        return (arranger, new GridView(elements, arranger.ElementPixelSize));
    }

    [Fact]
    public void IndexedImage_ViewOnly_RendersSameAsArranger()
    {
        var (arranger, view) = CreatePair();

        Assert.Equal(new IndexedImage(arranger).Image, new IndexedImage(view).Image);
        Assert.Equal(arranger.GetReferencedPalettes(), view.GetReferencedPalettes());
    }

    [Fact]
    public void ExportImage_ViewOnly_WritesSamePixelsAsArranger()
    {
        var (arranger, view) = CreatePair();
        var adapter = new ImageSharpFileAdapter();

        new IndexedImage(arranger).ExportImage(_path, adapter);
        var expected = adapter.LoadImage(_path).AsSuccess.Result;
        new IndexedImage(view).ExportImage(_path, adapter);
        var actual = adapter.LoadImage(_path).AsSuccess.Result;

        Assert.Equal(expected.Pixels, actual.Pixels);
    }

    [Fact]
    public void TrySetPalette_ViewOnly_FailsWithReason()
    {
        var (_, view) = CreatePair();
        var other = ArrangerTestFactory.CreatePalette(Enumerable.Range(0, 16).Select(i => new ColorRgba32((byte)i, 0, 0, 255)).ToArray());

        var result = new IndexedImage(view).TrySetPalette(0, 0, other, new CodecFactory(_palette, []));

        Assert.True(result.HasFailed);
        Assert.Contains("cannot be set", result.AsError.Reason);
    }
}
