using System;
using System.Collections.Generic;
using System.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using Xunit;

namespace ImageMagitek.UnitTests.Project;

public sealed class SerializationMapperTests
{
    private readonly ColorFactory _colorFactory = new();
    private readonly MemoryDataSource _source = new("data", 64);

    private List<IColorSourceModel> MapSources(params IColorSource[] sources)
    {
        var palette = new Palette("pal", _colorFactory, ColorModel.Bgr15, sources, false, PaletteStorageSource.ProjectXml, _source);
        var resourceMap = new Dictionary<IProjectResource, string> { [_source] = "/data" };

        return palette.MapToModel(resourceMap, _colorFactory).ColorSources;
    }

    private List<FileColorSourceModel> MapFileSources(params IColorSource[] sources) =>
        MapSources(sources).Cast<FileColorSourceModel>().ToList();

    private static void AssertRun(FileColorSourceModel model, long address, int entries, Endian endian) =>
        Assert.Equal((address, entries, endian), (model.FileAddress, model.Entries, model.Endian));

    private static ScatteredColorSource Nibble(long bitPosition, int shift = 0) =>
        new([new ColorField(bitPosition / 8, (int)(bitPosition % 8), 4, shift)]);

    [Fact]
    public void MapToModel_MixedEndianRun_SplitsAtEndianChange()
    {
        var models = MapFileSources(
            new FileColorSource(0, Endian.Little),
            new FileColorSource(2, Endian.Little),
            new FileColorSource(4, Endian.Big),
            new FileColorSource(6, Endian.Big));

        Assert.Equal(2, models.Count);
        AssertRun(models[0], 0, 2, Endian.Little);
        AssertRun(models[1], 4, 2, Endian.Big);
    }

    [Fact]
    public void MapToModel_GapInRun_SplitsAtGap()
    {
        var models = MapFileSources(
            new FileColorSource(0, Endian.Little),
            new FileColorSource(2, Endian.Little),
            new FileColorSource(8, Endian.Little));

        Assert.Equal(2, models.Count);
        AssertRun(models[0], 0, 2, Endian.Little);
        AssertRun(models[1], 8, 1, Endian.Little);
    }

    [Fact]
    public void MapToModel_ScatteredRun_MergesByStride()
    {
        var sources = Enumerable.Range(0, 4)
            .Select(n => (IColorSource)new ScatteredColorSource([new ColorField(0x10 + n, 0, 5, 0), new ColorField(0x20, 0, 4, 5) with { Offset = 0x20 + n / 2, LowBit = n % 2 * 4 }]))
            .ToArray();

        var model = Assert.IsType<ScatteredColorSourceModel>(Assert.Single(MapSources(sources)));

        Assert.Equal(4, model.Entries);
        Assert.Equal([new ColorFieldRun(new ColorField(0x10, 0, 5, 0), 8), new ColorFieldRun(new ColorField(0x20, 0, 4, 5), 4)], model.Fields);
    }

    [Fact]
    public void MapToModel_ScatteredMismatchedStride_SplitsRuns()
    {
        var models = MapSources(Nibble(0), Nibble(4), Nibble(8), Nibble(16), Nibble(24));

        var runs = models.Cast<ScatteredColorSourceModel>().Select(x => (x.Entries, x.Fields[0].First.BitPosition, x.Fields[0].Stride)).ToList();
        Assert.Equal([(3, 0L, 4), (2, 16L, 8)], runs);
    }

    [Fact]
    public void MapToModel_HighNibbleFirst_WritesRunsOfTwo()
    {
        var models = MapSources(Nibble(4), Nibble(0), Nibble(12), Nibble(8), Nibble(20), Nibble(16));

        var runs = models.Cast<ScatteredColorSourceModel>().Select(x => (x.Entries, x.Fields[0].First.BitPosition)).ToList();
        Assert.Equal([(1, 4L), (2, 0L), (2, 8L), (1, 16L)], runs);
    }

    [Fact]
    public void MapToModel_UnknownColorSource_ThrowsNamingType()
    {
        var ex = Assert.Throws<NotSupportedException>(() => MapSources(new UnknownColorSource()));

        Assert.Contains(nameof(UnknownColorSource), ex.Message);
    }

    private sealed class UnknownColorSource : IColorSource;
}
