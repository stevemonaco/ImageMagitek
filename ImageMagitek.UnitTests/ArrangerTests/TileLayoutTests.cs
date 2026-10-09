using System;
using System.Drawing;
using System.IO;
using System.Linq;
using ImageMagitek.Services;
using Xunit;

namespace ImageMagitek.UnitTests.ArrangerTests;

public class TileLayoutTests
{
    [Fact]
    public void Create_RowMajor_VisitsRowsFirst()
    {
        var layout = TileLayout.Create("3x2 H", 3, 2, columnMajor: false);

        Point[] expected = [new(0, 0), new(1, 0), new(2, 0), new(0, 1), new(1, 1), new(2, 1)];
        Assert.Equal(expected, layout.Pattern);
        Assert.Equal(6, layout.TilesPerPattern);
    }

    [Fact]
    public void Create_ColumnMajor_VisitsColumnsFirst()
    {
        var layout = TileLayout.Create("3x2 V", 3, 2, columnMajor: true);

        Point[] expected = [new(0, 0), new(0, 1), new(1, 0), new(1, 1), new(2, 0), new(2, 1)];
        Assert.Equal(expected, layout.Pattern);
    }

    [Fact]
    public void ShippedLayouts_AllDeserialize()
    {
        var layoutPath = Path.Combine(AppContext.BaseDirectory, "_layouts");
        var files = Directory.GetFiles(layoutPath, "*.json");
        var service = new ElementLayoutService();

        Assert.Contains(files, x => Path.GetFileName(x) == "2x2 V.json");

        foreach (var file in files)
        {
            var layout = service.ReadLayout(file).AsSuccess.Result;

            Assert.Equal(Path.GetFileNameWithoutExtension(file), layout.Name);
            Assert.Equal(layout.Width * layout.Height, layout.TilesPerPattern);
            Assert.Equal(layout.TilesPerPattern, layout.Pattern.Distinct().Count());
            Assert.All(layout.Pattern, p => Assert.True(p.X < layout.Width && p.Y < layout.Height));
        }
    }
}
