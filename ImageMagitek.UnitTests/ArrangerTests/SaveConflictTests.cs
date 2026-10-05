using System.Collections.Generic;
using System.Linq;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.ArrangerTests;

public class SaveConflictTests
{
    private static ScatteredArranger CreateDuplicateTileArranger()
    {
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1,
            (_, _) => CodecFixture.Shared.CodecFactory.CreateCodec("GBA 4bpp")!);
        var duplicate = arranger.GetElement(1, 0)!.Value.WithAddress(arranger.GetElement(0, 0)!.Value.SourceAddress);
        arranger.SetElement(duplicate, 1, 0);
        return arranger;
    }

    [Fact]
    public void AnalyzeSaveConflicts_UnchangedDuplicateOfModifiedTile_IsConflict()
    {
        var arranger = CreateDuplicateTileArranger();
        var encoded = new Dictionary<(int X, int Y), byte[]>
        {
            [(0, 0)] = Enumerable.Repeat((byte)0x11, 32).ToArray(),
            [(1, 0)] = new byte[32]
        };

        var conflicts = arranger.AnalyzeSaveConflicts(encoded);

        Assert.True(conflicts.HasConflicts);
    }

    [Fact]
    public void AnalyzeSaveConflicts_DuplicatesWithIdenticalEdits_IsNotConflict()
    {
        var arranger = CreateDuplicateTileArranger();
        var edited = Enumerable.Repeat((byte)0x11, 32).ToArray();
        var encoded = new Dictionary<(int X, int Y), byte[]> { [(0, 0)] = edited, [(1, 0)] = edited };

        var conflicts = arranger.AnalyzeSaveConflicts(encoded);

        Assert.False(conflicts.HasConflicts);
        Assert.True(conflicts.HasModifications);
    }
}
