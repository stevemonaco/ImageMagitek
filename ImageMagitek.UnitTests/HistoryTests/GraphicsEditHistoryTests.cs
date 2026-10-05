using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.PluginSample;
using ImageMagitek.UnitTests.TestFactories;
using TileShop.Shared.Models;
using TileShop.UI.Features.Graphics;
using TileShop.UI.Models;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.HistoryTests;

public class GraphicsEditHistoryTests
{
    private static readonly Palette _palette = CreatePalette(0);
    private static readonly Palette _otherPalette = CreatePalette(100);
    private static readonly CodecFactory _codecFactory = CreateCodecFactory();

    private static CodecFactory CreateCodecFactory()
    {
        var factory = new CodecFactory(_palette, []);
        factory.AddOrUpdateCodec(typeof(Psx4BppCodec));
        return factory;
    }

    private static Palette CreatePalette(int seed) =>
        ArrangerTestFactory.CreatePalette(Enumerable.Range(0, 16)
            .Select(i => new ColorRgba32((byte)(seed + i * 8), (byte)(seed + i * 4), (byte)(i * 2), 255))
            .ToArray());

    private static ArrangerImageAdapter CreateIndexed(int elementWidth = 16, int elementHeight = 8, Func<int, byte>? data = null)
    {
        const int elemsX = 3;
        const int elemsY = 2;
        var bytes = Enumerable.Range(0, elemsX * elemsY * elementWidth * elementHeight / 2)
            .Select(data ?? (i => (byte)(i * 37 + 11)))
            .ToArray();

        var source = new MemoryDataSource("test", bytes.Length);
        source.Write(new BitAddress(0), bytes);

        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, elemsX, elemsY,
            (_, _) => new Psx4BppCodec(_palette, elementWidth, elementHeight), source);
        return new ArrangerImageAdapter(arranger);
    }

    private static ArrangerImageAdapter CreateDirect(Func<int, byte>? data = null)
    {
        const int elemsX = 2;
        const int elemsY = 2;
        var bytes = Enumerable.Range(0, elemsX * elemsY * 8 * 8 * 4)
            .Select(data ?? (i => (byte)(i * 37 + 11)))
            .ToArray();

        var source = new MemoryDataSource("test", bytes.Length);
        source.Write(new BitAddress(0), bytes);

        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Direct, elemsX, elemsY,
            (_, _) => new Rgba32TiledCodec(8, 8), source);
        return new ArrangerImageAdapter(arranger);
    }

    [Fact]
    public void Pencil_Indexed_UndoRedo()
    {
        AssertUndoRedo(CreateIndexed(), image =>
        {
            var action = new PencilHistoryAction<byte>(5);
            foreach (var (x, y) in new[] { (1, 1), (2, 1), (20, 10), (47, 15) })
            {
                image.SetIndexedPixel(x, y, 5);
                action.Add(x, y);
            }
            return action;
        });
    }

    [Fact]
    public void Pencil_Direct_UndoRedo()
    {
        var color = new ColorRgba32(1, 2, 3, 255);
        AssertUndoRedo(CreateDirect(), image =>
        {
            var action = new PencilHistoryAction<ColorRgba32>(color);
            foreach (var (x, y) in new[] { (0, 0), (9, 3), (15, 15) })
            {
                image.SetDirectPixel(x, y, color);
                action.Add(x, y);
            }
            return action;
        });
    }

    [Fact]
    public void FloodFill_IndexedWithClip_UndoRedo()
    {
        var clip = new Rectangle(4, 2, 20, 10);
        AssertUndoRedo(CreateIndexed(data: _ => 0x11), image =>
        {
            Assert.True(image.FloodFill(10, 5, (byte)7, clip));
            return new FloodFillAction<byte>(10, 5, 7, clip);
        });
    }

    [Fact]
    public void FloodFill_DirectWithClip_UndoRedo()
    {
        var clip = new Rectangle(2, 2, 10, 6);
        var color = new ColorRgba32(10, 20, 30, 255);
        AssertUndoRedo(CreateDirect(_ => 0x80), image =>
        {
            Assert.True(image.FloodFill(5, 5, color, clip));
            return new FloodFillAction<ColorRgba32>(5, 5, color, clip);
        });
    }

    [Fact]
    public void ColorRemap_WithBounds_UndoRedo()
    {
        var remap = Enumerable.Range(0, 16).Select(i => (byte)(15 - i)).ToArray();
        var bounds = new Rectangle(8, 0, 16, 8);
        AssertUndoRedo(CreateIndexed(), image =>
        {
            image.RemapColors(remap, bounds);
            return new ColorRemapHistoryAction(remap, bounds);
        });
    }

    [Fact]
    public void PixelPaste_UndoRedo()
    {
        var clip = new Rectangle(16, 0, 24, 16);
        AssertUndoRedo(CreateIndexed(), image =>
        {
            var copy = image.Arranger.CopyPixelsIndexed(0, 0, 16, 8);
            Assert.True(GraphicsEditHistory.ApplyPixelPaste(image, copy, 20, 4, clip).HasSucceeded);
            return new PasteArrangerHistoryAction(copy, 20, 4, clip);
        });
    }

    [Fact]
    public void Mirror_UndoRedo()
    {
        AssertUndoRedo(CreateIndexed(), image => Mirror(image, 1, 0));
    }

    [Fact]
    public void Rotate_UndoRedo()
    {
        AssertUndoRedo(CreateIndexed(8, 8), image => Rotate(image, 2, 1));
    }

    [Fact]
    public void ApplyPalette_UndoRedo()
    {
        AssertUndoRedo(CreateIndexed(), image =>
        {
            Assert.True(image.TrySetPalette(16, 0, _otherPalette).HasSucceeded);
            var action = new ApplyPaletteHistoryAction(_otherPalette);
            action.Add(16, 0);
            return action;
        });
    }

    [Fact]
    public void DeleteNonSquareElements_UndoRedo()
    {
        AssertUndoRedo(CreateIndexed(16, 8), image =>
        {
            var rect = new SnappedRectangle(image.Arranger.ArrangerPixelSize, image.Arranger.ElementPixelSize, SnapMode.Element);
            rect.SetBounds(16, 48, 0, 16);
            GraphicsEditorViewModel.ResetElements(image.Arranger, rect);

            Assert.NotNull(image.Arranger.GetElement(0, 1));
            Assert.Null(image.Arranger.GetElement(1, 1));
            Assert.Null(image.Arranger.GetElement(2, 0));

            image.Render();
            return new DeleteElementSelectionHistoryAction(rect);
        });
    }

    [Fact]
    public void ApplyPaletteAfterPixelPaste_UndoRedo_KeepsPaste()
    {
        var image = CreateIndexed();
        var history = new GraphicsEditHistory(new(), new(), image.Arranger, _codecFactory);

        var copy = image.Arranger.CopyPixelsIndexed(0, 0, 16, 8);
        Assert.True(GraphicsEditHistory.ApplyPixelPaste(image, copy, 16, 8, null).HasSucceeded);
        history.Add(new PasteArrangerHistoryAction(copy, 16, 8, null), image.Arranger);
        var pasted = EditorState.Capture(image);

        Assert.True(image.TrySetPalette(16, 0, _otherPalette).HasSucceeded);
        var applyPalette = new ApplyPaletteHistoryAction(_otherPalette);
        applyPalette.Add(16, 0);
        history.Add(applyPalette, image.Arranger);
        var applied = EditorState.Capture(image);

        history.Undo(image);
        pasted.AssertMatches(image);

        history.Redo(image);
        applied.AssertMatches(image);

        var pencil = new PencilHistoryAction<byte>(3);
        image.SetIndexedPixel(0, 0, 3);
        pencil.Add(0, 0);
        history.Add(pencil, image.Arranger);

        history.Undo(image);
        applied.AssertMatches(image);
    }

    [Fact]
    public void ApplyPalette_Undo_DoesNotModifySharedCodec()
    {
        var image = CreateIndexed();
        var history = new GraphicsEditHistory(new(), new(), image.Arranger, _codecFactory);
        var sharedCodec = (IIndexedCodec)image.Arranger.GetElement(1, 0)!.Value.Codec;

        Assert.True(image.TrySetPalette(16, 0, _otherPalette).HasSucceeded);
        var action = new ApplyPaletteHistoryAction(_otherPalette);
        action.Add(16, 0);
        history.Add(action, image.Arranger);

        history.Undo(image);

        Assert.Same(_otherPalette, sharedCodec.Palette);
        Assert.Same(_palette, ((IIndexedCodec)image.Arranger.GetElement(1, 0)!.Value.Codec).Palette);
    }

    [Fact]
    public void Resize_UndoRedo()
    {
        AssertUndoRedo(CreateIndexed(), image =>
        {
            image.Arranger.Resize(2, 1);
            image.Reinitialize(image.Arranger);
            return new ResizeArrangerHistoryAction(2, 1);
        });
    }

    [Fact]
    public void ElementPaste_UndoRedo()
    {
        AssertUndoRedo(CreateIndexed(), image =>
        {
            var copy = image.Arranger.CopyElements(0, 0, 2, 1);
            var result = ElementCopier.CopyElements(copy, (ScatteredArranger)image.Arranger, new Point(0, 0), new Point(1, 1), 2, 1);
            Assert.True(result.HasSucceeded);
            image.Render();
            return new ElementPasteHistoryAction();
        });
    }

    [Fact]
    public void Add_ClearsRedo()
    {
        var image = CreateIndexed();
        var undo = new ObservableCollection<HistoryAction>();
        var redo = new ObservableCollection<HistoryAction>();
        var history = new GraphicsEditHistory(undo, redo, image.Arranger, _codecFactory);

        history.Add(Mirror(image, 0, 0), image.Arranger);
        history.Undo(image);
        Assert.NotEmpty(redo);

        history.Add(Mirror(image, 1, 0), image.Arranger);

        Assert.Empty(redo);
        Assert.NotEmpty(undo);
    }

    [Fact]
    public void MirrorThenRotate_UndoUndoRedoRedo_RestoresEachState()
    {
        var image = CreateIndexed(8, 8);
        var history = new GraphicsEditHistory(new(), new(), image.Arranger, _codecFactory);
        var original = EditorState.Capture(image);

        history.Add(Mirror(image, 0, 0), image.Arranger);
        var mirrored = EditorState.Capture(image);

        history.Add(Rotate(image, 1, 0), image.Arranger);
        var rotated = EditorState.Capture(image);

        Assert.True(history.Undo(image));
        mirrored.AssertMatches(image);

        Assert.True(history.Undo(image));
        original.AssertMatches(image);

        Assert.True(history.Redo(image));
        mirrored.AssertMatches(image);

        Assert.True(history.Redo(image));
        rotated.AssertMatches(image);
    }

    [Fact]
    public void PixelEditAfterArrangerAction_Undo_ReplaysFromSnapshot()
    {
        var image = CreateIndexed();
        var history = new GraphicsEditHistory(new(), new(), image.Arranger, _codecFactory);

        history.Add(Mirror(image, 1, 0), image.Arranger);
        var mirrored = EditorState.Capture(image);

        var pencil = new PencilHistoryAction<byte>(3);
        image.SetIndexedPixel(0, 0, 3);
        pencil.Add(0, 0);
        history.Add(pencil, image.Arranger);

        Assert.False(history.Undo(image));
        mirrored.AssertMatches(image);
    }

    private static MirrorElementHistoryAction Mirror(ArrangerImageAdapter image, int elementX, int elementY)
    {
        Assert.True(image.Arranger.TryMirrorElement(elementX, elementY, MirrorOperation.Horizontal).HasSucceeded);
        image.Render();
        return new MirrorElementHistoryAction(elementX, elementY, MirrorOperation.Horizontal);
    }

    private static RotateElementHistoryAction Rotate(ArrangerImageAdapter image, int elementX, int elementY)
    {
        Assert.True(image.Arranger.TryRotateElement(elementX, elementY, RotationOperation.Left).HasSucceeded);
        image.Render();
        return new RotateElementHistoryAction(elementX, elementY, RotationOperation.Left);
    }

    private static void AssertUndoRedo(ArrangerImageAdapter image, Func<ArrangerImageAdapter, HistoryAction> perform)
    {
        var undo = new ObservableCollection<HistoryAction>();
        var redo = new ObservableCollection<HistoryAction>();
        var history = new GraphicsEditHistory(undo, redo, image.Arranger, _codecFactory);
        var before = EditorState.Capture(image);

        var action = perform(image);
        history.Add(action, image.Arranger);
        var after = EditorState.Capture(image);
        Assert.NotNull(before.Difference(after));

        history.Undo(image);
        before.AssertMatches(image);
        Assert.Empty(undo);

        history.Redo(image);
        after.AssertMatches(image);
        Assert.Empty(redo);
    }

    private sealed record ElementState(DataSource? Source, BitAddress Address, MirrorOperation Mirror, RotationOperation Rotation, Palette? Palette);

    private sealed class EditorState
    {
        private readonly Size _elementSize;
        private readonly ElementState?[,] _elements;
        private readonly byte[]? _indexed;
        private readonly ColorRgba32[]? _direct;

        private EditorState(Size elementSize, ElementState?[,] elements, byte[]? indexed, ColorRgba32[]? direct)
        {
            _elementSize = elementSize;
            _elements = elements;
            _indexed = indexed;
            _direct = direct;
        }

        public static EditorState Capture(ArrangerImageAdapter image)
        {
            var arranger = image.Arranger;
            var size = arranger.ArrangerElementSize;
            var elements = new ElementState?[size.Height, size.Width];

            for (int y = 0; y < size.Height; y++)
            {
                for (int x = 0; x < size.Width; x++)
                {
                    if (arranger.GetElement(x, y) is { } el)
                        elements[y, x] = new ElementState(el.Source, el.SourceAddress, el.Mirror, el.Rotation, (el.Codec as IIndexedCodec)?.Palette);
                }
            }

            return new EditorState(size, elements, image.IndexedImage?.Image.ToArray(), image.DirectImage?.Image.ToArray());
        }

        public string? Difference(EditorState other)
        {
            if (_elementSize != other._elementSize)
                return $"Arranger size differs. Expected {_elementSize}, actual {other._elementSize}";

            for (int y = 0; y < _elementSize.Height; y++)
            {
                for (int x = 0; x < _elementSize.Width; x++)
                {
                    var expected = _elements[y, x];
                    var actual = other._elements[y, x];

                    if (expected is null != actual is null || expected is not null && actual is not null &&
                        (!ReferenceEquals(expected.Source, actual.Source) || expected.Address != actual.Address ||
                         expected.Mirror != actual.Mirror || expected.Rotation != actual.Rotation ||
                         !ReferenceEquals(expected.Palette, actual.Palette)))
                    {
                        return $"Element ({x}, {y}) differs. Expected {expected}, actual {actual}";
                    }
                }
            }

            if (!SequenceEqual(_indexed, other._indexed) || !SequenceEqual(_direct, other._direct))
                return "Pixels differ";

            return null;
        }

        public void AssertMatches(ArrangerImageAdapter image) => Assert.Null(Difference(Capture(image)));

        private static bool SequenceEqual<T>(T[]? a, T[]? b) =>
            a is null ? b is null : b is not null && a.SequenceEqual(b);
    }
}
