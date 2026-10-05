using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ImageMagitek.Colors;
using TileShop.Shared.Models;
using TileShop.UI.Features.Palettes;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.PaletteEditorTests;

public class PaletteEditSessionTests
{
    private static readonly byte[] _fileBytes = [0x00, 0x00, 0x1F, 0x00, 0xE0, 0x03, 0x00, 0x7C, 0xFF, 0x7F, 0x10, 0x42];
    private const int _fileColors = 4;
    private const int _nativeIndex = 4;
    private const int _foreignIndex = 5;

    private readonly ColorFactory _colorFactory = new();
    private readonly MemoryDataSource _source;
    private readonly Palette _palette;
    private readonly ObservableCollection<HistoryAction> _undo = [];
    private readonly ObservableCollection<HistoryAction> _redo = [];
    private readonly PaletteEditSession _session;

    public PaletteEditSessionTests()
    {
        _source = new MemoryDataSource("test", _fileBytes.Length);
        _source.Write(new BitAddress(0), _fileBytes);

        var sources = Enumerable.Range(0, _fileColors)
            .Select(i => (IColorSource)new FileColorSource(new BitAddress(i * 2, 0), Endian.Little))
            .Append(new ProjectNativeColorSource(new ColorRgba32(10, 20, 30, 255)))
            .Append(new ProjectForeignColorSource(new ColorBgr15(0x1234)))
            .ToList();

        _palette = new Palette("test", _colorFactory, ColorModel.Bgr15, sources, false, PaletteStorageSource.ProjectXml, _source);
        _session = new PaletteEditSession(_palette, _colorFactory, _undo, _redo);
    }

    private byte[] ReadFile() => _source.Read(new BitAddress(0), _fileBytes.Length * 8);

    private uint ColorAt(int index) => _palette.GetForeignColor(index).Color;

    private uint[] AllColors() => Enumerable.Range(0, _palette.Entries).Select(ColorAt).ToArray();

    [Fact]
    public void SetColor_UndoRedoCommit_WritesFileOnlyOnCommit()
    {
        _session.SetColor(1, new ColorBgr15(0x7C1F));

        Assert.Equal(0x7C1Fu, ColorAt(1));
        Assert.Equal(_fileBytes, ReadFile());
        Assert.True(_session.IsModified);

        _session.Undo();
        Assert.Equal(0x001Fu, ColorAt(1));
        Assert.False(_session.IsModified);
        Assert.True(_session.CanRedo);

        _session.Redo();
        Assert.Equal(0x7C1Fu, ColorAt(1));
        Assert.True(_session.IsModified);

        Assert.True(_session.Commit());
        Assert.False(_session.IsModified);
        Assert.False(_session.CanRedo);
        Assert.Equal(new byte[] { 0x1F, 0x7C }, ReadFile()[2..4]);

        _palette.Reload();
        Assert.Equal(0x7C1Fu, ColorAt(1));
    }

    [Fact]
    public void Discard_RestoresColorsSourcesAndTransparency()
    {
        var original = AllColors();

        _session.SetColor(0, new ColorBgr15(0x7FFF));
        _session.SetZeroIndexTransparent(true);
        _session.SetSources([new FileColorSource(new BitAddress(8, 0), Endian.Little)]);

        _session.Discard();

        Assert.Equal(original, AllColors());
        Assert.Equal(6, _palette.Entries);
        Assert.False(_palette.ZeroIndexTransparent);
        Assert.False(_session.IsModified);
        Assert.Equal(_fileBytes, ReadFile());
    }

    [Fact]
    public void SetSources_OffsetEdit_KeepsPendingColorsOnUnchangedFileEntries()
    {
        _session.SetColor(0, new ColorBgr15(0x0421));

        var sources = _palette.ColorSources.ToList();
        sources[1] = new FileColorSource(new BitAddress(8, 0), Endian.Little);
        _session.SetSources(sources);

        Assert.Equal(0x0421u, ColorAt(0));
        Assert.Equal(0x7FFFu, ColorAt(1));
        Assert.Equal(_fileBytes, ReadFile());
    }

    [Fact]
    public void SetColor_ProjectNative_UpdatesSourceValueInMemoryUntilDiscard()
    {
        var nativeSource = (ProjectNativeColorSource)_palette.ColorSources[_nativeIndex];
        var original = nativeSource.Value;
        var edited = new ColorBgr15(0x001F);

        _session.SetColor(_nativeIndex, edited);

        var current = (ProjectNativeColorSource)_palette.ColorSources[_nativeIndex];
        Assert.Equal(_colorFactory.ToNative(edited), current.Value);
        Assert.Equal(_fileBytes, ReadFile());

        _session.Discard();

        var restored = (ProjectNativeColorSource)_palette.ColorSources[_nativeIndex];
        Assert.Equal(original, restored.Value);
    }

    [Fact]
    public void SetColor_ProjectForeign_CommitKeepsValue()
    {
        _session.SetColor(_foreignIndex, new ColorBgr15(0x0123));
        _session.Commit();

        var source = (ProjectForeignColorSource)_palette.ColorSources[_foreignIndex];
        Assert.Equal(0x0123u, source.Value.Color);

        _palette.Reload();
        Assert.Equal(0x0123u, ColorAt(_foreignIndex));
    }

    [Fact]
    public void SwapColors_IsOneUndoStep()
    {
        var original = AllColors();

        _session.SwapColors(1, 3);

        Assert.Single(_undo);
        Assert.Equal(original[3], ColorAt(1));
        Assert.Equal(original[1], ColorAt(3));

        _session.Undo();
        Assert.Equal(original, AllColors());
    }

    [Fact]
    public void FillGradient_InterpolatesBetweenEnds_AsOneUndoStep()
    {
        var original = AllColors();
        var start = _colorFactory.ToNative(_palette.GetForeignColor(0));
        var end = _colorFactory.ToNative(_palette.GetForeignColor(3));

        _session.FillGradient([0, 1, 2, 3]);

        Assert.Single(_undo);
        var expected = _colorFactory.ToForeign(new ColorRgba32(
            Lerp(start.R, end.R, 1 / 3.0), Lerp(start.G, end.G, 1 / 3.0), Lerp(start.B, end.B, 1 / 3.0), Lerp(start.A, end.A, 1 / 3.0)),
            ColorModel.Bgr15);
        Assert.Equal(expected.Color, ColorAt(1));
        Assert.Equal(original[0], ColorAt(0));
        Assert.Equal(original[3], ColorAt(3));

        _session.Undo();
        Assert.Equal(original, AllColors());
    }

    [Fact]
    public void SetColors_PastesFromStartAndClampsToPalette_AsOneUndoStep()
    {
        var original = AllColors();
        var colors = new List<IColor> { new ColorBgr15(0x0001), new ColorBgr15(0x0002), new ColorBgr15(0x0003) };

        _session.SetColors(4, colors);

        Assert.Single(_undo);
        Assert.Equal(0x0002u, ColorAt(5));
        Assert.Equal(original[..4], AllColors()[..4]);

        _session.Undo();
        Assert.Equal(original, AllColors());
    }

    [Fact]
    public void ChangeColorModel_ThenUndo_RestoresModelAndColors()
    {
        var original = AllColors();
        var sources = ChangeColorModelViewModel.ReinterpretSources(_palette, ColorModel.Rgb15, _colorFactory);

        _session.ChangeColorModel(ColorModel.Rgb15, sources);

        Assert.Equal(ColorModel.Rgb15, _palette.ColorModel);
        Assert.IsType<ColorRgb15>(_palette.GetForeignColor(0));
        Assert.Equal(0x1234u, ColorAt(_foreignIndex));

        _session.Undo();

        Assert.Equal(ColorModel.Bgr15, _palette.ColorModel);
        Assert.IsType<ColorBgr15>(_palette.GetForeignColor(0));
        Assert.Equal(original, AllColors());
    }

    [Fact]
    public void ReinterpretSources_ToLargerColor_RespacesFileOffsets()
    {
        var sources = ChangeColorModelViewModel.ReinterpretSources(_palette, ColorModel.Rgba32, _colorFactory);

        var offsets = sources.OfType<FileColorSource>().Select(x => x.Offset.ByteOffset).ToArray();
        Assert.Equal(new long[] { 0, 4, 8, 12 }, offsets);
    }

    [Fact]
    public void IsModified_TracksHistoryAcrossSaveUndoAndRedo()
    {
        Assert.False(_session.IsModified);

        _session.SetColor(0, new ColorBgr15(0x0001));
        _session.SetColor(0, new ColorBgr15(0x0002));
        Assert.True(_session.IsModified);

        _session.Undo();
        Assert.True(_session.IsModified);
        Assert.Equal(0x0001u, ColorAt(0));

        _session.Undo();
        Assert.False(_session.IsModified);

        _session.Redo();
        Assert.True(_session.IsModified);

        _session.Commit();
        Assert.False(_session.IsModified);

        _session.SetColor(0, new ColorBgr15(0x0003));
        _session.Undo();
        Assert.Equal(0x0001u, ColorAt(0));
    }

    [Fact]
    public void SetSources_Unchanged_AddsNoHistory()
    {
        _session.SetSources(_palette.ColorSources.Select(x => PaletteSnapshot.CloneSource(x, _colorFactory)).ToList());

        Assert.Empty(_undo);
    }

    private static byte Lerp(byte a, byte b, double t) => (byte)System.Math.Round(a + (b - a) * t);
}
