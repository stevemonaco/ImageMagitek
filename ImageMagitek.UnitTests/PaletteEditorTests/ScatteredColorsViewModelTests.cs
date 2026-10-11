using System;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;
using TileShop.UI.Services;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.PaletteEditorTests;

public sealed class ScatteredColorsViewModelTests
{
    private static readonly byte[] _fileBytes = [0x1F, 0x00, 0x10, 0x00, 0x05, 0x7C, 0x34, 0x12];

    private readonly ColorFactory _colorFactory = new();
    private readonly MemoryDataSource _source;
    private readonly Palette _palette;

    public ScatteredColorsViewModelTests()
    {
        _source = new MemoryDataSource("test", _fileBytes.Length);
        _source.Write(new BitAddress(0), _fileBytes);

        var sources = ScatteredColorSource.ExpandRun(2, [new ColorFieldRun(new ColorField(0, 0, 8, 0), 16), new ColorFieldRun(new ColorField(1, 0, 7, 8), 16)]);
        _palette = new Palette("test", _colorFactory, ColorModel.Bgr15, sources.ToList<IColorSource>(), false, PaletteStorageSource.ProjectXml, _source);
    }

    [Fact]
    public void NewRun_DefaultsToLittleEndianLayout()
    {
        var dialog = new ScatteredColorsViewModel(_palette, _colorFactory, null);

        Assert.Equal(1, dialog.Entries);
        var fields = dialog.Fields.Select(f => (f.OffsetText, f.LowBit, f.Bits, f.Shift, f.Stride)).ToList();
        Assert.Equal([("0", 0, 8, 0, 8), ("1", 0, 8, 8, 8)], fields);
        Assert.Equal(_colorFactory.ToNative(new ColorBgr15(0x001F)), ToRgba(Assert.Single(dialog.PreviewColors).Color));
        Assert.True(dialog.TryAcceptCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("offset does not parse", 0)]
    [InlineData("low bit out of range", 1)]
    [InlineData("bits out of range", 1)]
    [InlineData("low bit plus bits over 8", 1)]
    [InlineData("does not fit the model", 1)]
    [InlineData("overlaps another field", 1)]
    [InlineData("crosses a byte", 1)]
    [InlineData("past the end of the file", 0)]
    public void InvalidField_DisablesOkAndClearsPreview(string name, int field)
    {
        var dialog = new ScatteredColorsViewModel(_palette, _colorFactory, null);
        var f = dialog.Fields[field];

        switch (name)
        {
            case "offset does not parse": f.OffsetText = "zz"; break;
            case "low bit out of range": f.LowBit = 8; break;
            case "bits out of range": f.Bits = 0; break;
            case "low bit plus bits over 8": f.LowBit = 4; break;
            case "does not fit the model": f.Shift = 9; break;
            case "overlaps another field": f.OffsetText = "0"; f.Shift = 8; f.Bits = 4; break;
            case "crosses a byte": dialog.Entries = 2; f.Stride = 4; break;
            case "past the end of the file": f.OffsetText = "8"; break;
        }

        Assert.NotEqual("", f.Error);
        Assert.Empty(dialog.PreviewColors);
        Assert.False(dialog.TryAcceptCommand.CanExecute(null));
    }

    [Fact]
    public void Preview_UpdatesOnFieldChange()
    {
        var dialog = new ScatteredColorsViewModel(_palette, _colorFactory, null);

        dialog.Fields[0].OffsetText = "6";
        dialog.Fields[1].OffsetText = "7";

        Assert.Equal(_colorFactory.ToNative(new ColorBgr15(0x1234)), ToRgba(Assert.Single(dialog.PreviewColors).Color));
    }

    [Fact]
    public async Task Ok_ReplacesRunAsOneStep()
    {
        var interactions = new DialogInteractionService(dialog =>
        {
            dialog.Entries = 3;
            dialog.Fields[0].Stride = 8;
            dialog.Fields[1].OffsetText = "5";
            dialog.Fields[1].Stride = 8;
        });
        var editor = new PaletteEditorViewModel(_palette, _colorFactory, new ProjectService(ProjectFixtures.CreateSerializerFactory(_colorFactory)),
            interactions, null!, new ClipboardService());
        var row = Assert.IsType<ScatteredColorSourceModel>(Assert.Single(editor.ColorSourceModels));

        await editor.EditScatteredColorSourceCommand.ExecuteAsync(row);

        var replaced = Assert.IsType<ScatteredColorSourceModel>(Assert.Single(editor.ColorSourceModels));
        Assert.Equal(3, replaced.Entries);
        Assert.Equal("Edit sources", Assert.Single(editor.UndoHistory).Name);
        Assert.Equal(3, _palette.Entries);
        Assert.Equal([0x7C1Fu, 0x3400u, 0x1210u], Enumerable.Range(0, 3).Select(i => _palette.GetForeignColor(i).Color));
    }

    private static ColorRgba32 ToRgba(Avalonia.Media.Color color) => new(color.R, color.G, color.B, color.A);

    private sealed class DialogInteractionService(Action<ScatteredColorsViewModel> edit) : IInteractionService
    {
        public Task AlertAsync(string heading, string message) => Task.CompletedTask;

        public Task<PromptResult> PromptAsync(PromptChoice choices, string heading, string message) => throw new NotSupportedException();

        public Task<TResult?> RequestAsync<TResult>(IRequestMediator<TResult> mediator)
        {
            var dialog = (ScatteredColorsViewModel)mediator;
            edit(dialog);
            Assert.True(dialog.TryAcceptCommand.CanExecute(null));
            return Task.FromResult((TResult?)(object?)dialog.ProduceResult());
        }
    }
}
