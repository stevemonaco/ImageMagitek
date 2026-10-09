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

    private List<FileColorSourceModel> MapFileSources(params IColorSource[] sources)
    {
        var palette = new Palette("pal", _colorFactory, ColorModel.Bgr15, sources, false, PaletteStorageSource.ProjectXml, _source);
        var resourceMap = new Dictionary<IProjectResource, string> { [_source] = "/data" };

        return palette.MapToModel(resourceMap, _colorFactory).ColorSources.Cast<FileColorSourceModel>().ToList();
    }

    private static void AssertRun(FileColorSourceModel model, BitAddress address, int entries, Endian endian) =>
        Assert.Equal((address, entries, endian), (model.FileAddress, model.Entries, model.Endian));

    [Fact]
    public void MapToModel_MixedEndianRun_SplitsAtEndianChange()
    {
        var models = MapFileSources(
            new FileColorSource(new BitAddress(0, 0), Endian.Little),
            new FileColorSource(new BitAddress(2, 0), Endian.Little),
            new FileColorSource(new BitAddress(4, 0), Endian.Big),
            new FileColorSource(new BitAddress(6, 0), Endian.Big));

        Assert.Equal(2, models.Count);
        AssertRun(models[0], new BitAddress(0, 0), 2, Endian.Little);
        AssertRun(models[1], new BitAddress(4, 0), 2, Endian.Big);
    }

    [Fact]
    public void MapToModel_BitOffsetRun_KeepsBitAddress()
    {
        var models = MapFileSources(
            new FileColorSource(new BitAddress(1, 4), Endian.Little),
            new FileColorSource(new BitAddress(3, 4), Endian.Little),
            new FileColorSource(new BitAddress(5, 4), Endian.Little));

        AssertRun(Assert.Single(models), new BitAddress(1, 4), 3, Endian.Little);
    }

    [Fact]
    public void MapToModel_GapInRun_SplitsAtGap()
    {
        var models = MapFileSources(
            new FileColorSource(new BitAddress(0, 0), Endian.Little),
            new FileColorSource(new BitAddress(2, 0), Endian.Little),
            new FileColorSource(new BitAddress(8, 0), Endian.Little));

        Assert.Equal(2, models.Count);
        AssertRun(models[0], new BitAddress(0, 0), 2, Endian.Little);
        AssertRun(models[1], new BitAddress(8, 0), 1, Endian.Little);
    }
}
