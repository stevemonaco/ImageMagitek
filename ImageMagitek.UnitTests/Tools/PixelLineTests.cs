using System;
using System.Drawing;
using System.Linq;
using TileShop.Shared.Tools;
using Xunit;

namespace ImageMagitek.UnitTests.Tools;

public class PixelLineTests
{
    [Fact]
    public void Points_SamePoint_YieldsOnePoint()
    {
        var points = PixelLine.Points(new Point(3, 4), new Point(3, 4)).ToList();

        Assert.Equal([new Point(3, 4)], points);
    }

    [Fact]
    public void Points_Horizontal_YieldsEveryColumn()
    {
        var points = PixelLine.Points(new Point(2, 5), new Point(6, 5)).ToList();

        Assert.Equal(Enumerable.Range(2, 5).Select(x => new Point(x, 5)), points);
    }

    [Fact]
    public void Points_Vertical_YieldsEveryRow()
    {
        var points = PixelLine.Points(new Point(5, 7), new Point(5, 3)).ToList();

        Assert.Equal(Enumerable.Range(3, 5).Reverse().Select(y => new Point(5, y)), points);
    }

    [Theory]
    [InlineData(0, 0, 10, 3)]
    [InlineData(0, 0, 3, 10)]
    [InlineData(0, 0, -3, 10)]
    [InlineData(0, 0, -10, 3)]
    [InlineData(0, 0, -10, -3)]
    [InlineData(0, 0, -3, -10)]
    [InlineData(0, 0, 3, -10)]
    [InlineData(0, 0, 10, -3)]
    [InlineData(10, 3, 0, 0)]
    [InlineData(5, 9, -7, 2)]
    [InlineData(1, 1, 40, 13)]
    [InlineData(40, 13, 1, 1)]
    [InlineData(0, 0, 7, 7)]
    [InlineData(0, 0, -7, 7)]
    public void Points_AnyOctant_IsContinuousInclusiveAndMinimal(int x0, int y0, int x1, int y1)
    {
        var start = new Point(x0, y0);
        var end = new Point(x1, y1);

        var points = PixelLine.Points(start, end).ToList();

        Assert.Equal(start, points[0]);
        Assert.Equal(end, points[^1]);
        Assert.Equal(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) + 1, points.Count);
        Assert.Equal(points.Count, points.Distinct().Count());

        foreach (var (a, b) in points.Zip(points.Skip(1)))
            Assert.Equal(1, Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)));
    }
}
