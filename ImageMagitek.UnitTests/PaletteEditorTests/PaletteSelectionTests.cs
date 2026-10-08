using TileShop.UI.Features.Palettes;
using Xunit;

namespace ImageMagitek.UnitTests.PaletteEditorTests;

public class PaletteSelectionTests
{
    [Fact]
    public void Click_SelectsOnlyThatIndex()
    {
        var selection = new PaletteSelection();
        selection.Click(3);
        selection.Click(5);

        Assert.Equal(new[] { 5 }, selection.Indices);
        Assert.Equal(5, selection.First);
    }

    [Fact]
    public void ShiftClick_SelectsRangeFromAnchorInEitherDirection()
    {
        var selection = new PaletteSelection();
        selection.Click(6);
        selection.ShiftClick(3);

        Assert.Equal(new[] { 3, 4, 5, 6 }, selection.Indices);

        selection.ShiftClick(8);
        Assert.Equal(new[] { 6, 7, 8 }, selection.Indices);
    }

    [Fact]
    public void ClickFirstThenShiftClickLast_SelectsAllAndReplacesScatteredSelection()
    {
        var selection = new PaletteSelection();
        selection.Click(2);
        selection.CtrlClick(5);

        selection.Click(0);
        selection.ShiftClick(7);

        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, selection.Indices);
    }

    [Fact]
    public void CtrlClick_TogglesIndices()
    {
        var selection = new PaletteSelection();
        selection.Click(1);
        selection.CtrlClick(4);
        selection.CtrlClick(9);
        selection.CtrlClick(1);

        Assert.Equal(new[] { 4, 9 }, selection.Indices);
    }

    [Fact]
    public void Move_ClampsToPalette()
    {
        var selection = new PaletteSelection();
        selection.Click(0);

        selection.Move(-1, 0, 16, 20);
        Assert.Equal(new[] { 0 }, selection.Indices);

        selection.Move(0, 1, 16, 20);
        Assert.Equal(new[] { 16 }, selection.Indices);

        selection.Move(0, 1, 16, 20);
        Assert.Equal(new[] { 19 }, selection.Indices);
    }

    [Fact]
    public void Move_Extend_SelectsRangeFromAnchor()
    {
        var selection = new PaletteSelection();
        selection.Click(2);
        selection.Move(1, 0, 16, 32, extend: true);
        selection.Move(1, 0, 16, 32, extend: true);

        Assert.Equal(new[] { 2, 3, 4 }, selection.Indices);
    }

    [Fact]
    public void Clamp_DropsIndicesPastCount()
    {
        var selection = new PaletteSelection();
        selection.Click(2);
        selection.ShiftClick(10);

        selection.Clamp(5);

        Assert.Equal(new[] { 2, 3, 4 }, selection.Indices);
    }
}
