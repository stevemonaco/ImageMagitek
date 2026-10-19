using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Compression;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Monaco.PathTree;
using Xunit;

namespace ImageMagitek.UnitTests.Compression;

public sealed class CompressedDataProjectTests : IAsyncLifetime
{
    // At offset 0x10: a GBA LZ77 block decompressing to 8 bytes of 0x41
    private static readonly byte[] _rom = [.. new byte[0x10], 0x10, 0x08, 0x00, 0x00, 0x00, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41];

    private readonly string _directory = TestPaths.CreateTempPath("");
    private readonly Palette _palette = ArrangerTestFactory.CreatePalette(new ColorRgba32(0, 0, 0, 255));
    private readonly CompressorFactory _compressorFactory = new();
    private readonly ProjectService _service;
    private ProjectTree _tree = null!;

    public CompressedDataProjectTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "rom.bin"), _rom);

        var serializerFactory = new XmlProjectSerializerFactory(Path.Combine(AppContext.BaseDirectory, "_schemas", "ResourceSchema.xsd"),
            CodecFixture.Shared.CodecFactory, new ColorFactory(), [_palette], _compressorFactory);
        _service = new ProjectService(serializerFactory);
    }

    public async Task InitializeAsync() =>
        _tree = (await _service.CreateNewProjectAsync(Path.Combine(_directory, "project.xml"))).AsSuccess.Result;

    public Task DisposeAsync()
    {
        _service.CloseProjects();

        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
        }

        return Task.CompletedTask;
    }

    private async Task<(FileDataSource Rom, CompressedDataSource Compressed)> AddRomAndCompressedAsync()
    {
        var rom = new FileDataSource("Rom", Path.Combine(_directory, "rom.bin"));
        Assert.True((await _service.AddResourceAsync(_tree.Root, rom)).HasSucceeded);

        var compressed = new CompressedDataSource("Packed", rom, 0x10, "GBA LZ77", _compressorFactory);
        Assert.True((await _service.AddResourceAsync(_tree.Root, compressed)).HasSucceeded);
        return (rom, compressed);
    }

    private ScatteredArranger CreateSprite(DataSource source)
    {
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 1, 1, (_, _) =>
        {
            var codec = (IIndexedCodec)CodecFixture.Shared.CodecFactory.CreateCodec("NES 1bpp")!;
            codec.Palette = _palette;
            return codec;
        }, source);
        arranger.Name = "Sprite";
        return arranger;
    }

    [Fact]
    public async Task SaveAndReload_RestoresCompressedDataAndArrangerReferences()
    {
        var (_, compressed) = await AddRomAndCompressedAsync();
        var arranger = CreateSprite(compressed);
        Assert.True((await _service.AddResourceAsync(_tree.Root, arranger)).HasSucceeded);

        var xml = File.ReadAllText(Path.Combine(_directory, "Packed.xml"));
        Assert.Contains("<compresseddata parent=\"/Rom\" fileoffset=\"10\" compression=\"GBA LZ77\"", xml);

        _service.CloseProjects();
        var reopened = (await _service.OpenProjectFileAsync(Path.Combine(_directory, "project.xml"))).AsSuccess.Result;

        Assert.True(reopened.TryGetItem<CompressedDataSource>("/Packed", out var loaded));
        Assert.Equal(Enumerable.Repeat((byte)0x41, 8), loaded.Read(new BitAddress(0, 0), 64));
        Assert.True(reopened.TryGetItem<ScatteredArranger>("/Sprite", out var loadedArranger));
        Assert.Same(loaded, loadedArranger.GetElement(0, 0)!.Value.Source);
    }

    [Fact]
    public async Task Reload_UnknownCompressor_LoadsWithError()
    {
        await AddRomAndCompressedAsync();
        File.WriteAllText(Path.Combine(_directory, "Packed.xml"),
            """<compresseddata parent="/Rom" fileoffset="10" compression="Not Installed" />""");

        _service.CloseProjects();
        var reopened = (await _service.OpenProjectFileAsync(Path.Combine(_directory, "project.xml"))).AsSuccess.Result;

        Assert.True(reopened.TryGetItem<CompressedDataSource>("/Packed", out var loaded));
        Assert.Contains("Not Installed", loaded.DecompressionError);
    }

    [Fact]
    public async Task Reload_ParentMissingFromProject_FailsNamingParent()
    {
        await AddRomAndCompressedAsync();
        File.WriteAllText(Path.Combine(_directory, "Packed.xml"),
            """<compresseddata parent="/Gone" fileoffset="10" compression="GBA LZ77" />""");

        _service.CloseProjects();
        var result = await _service.OpenProjectFileAsync(Path.Combine(_directory, "project.xml"));

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains("/Gone"));
    }

    [Fact]
    public async Task DeleteParent_RemovesCompressedDataAndItsArranger()
    {
        var (_, compressed) = await AddRomAndCompressedAsync();
        var arranger = CreateSprite(compressed);
        await _service.AddResourceAsync(_tree.Root, arranger);
        Assert.True(_tree.TryGetNode("/Rom", out var romNode));

        var plan = _service.PreviewResourceDeletion(romNode).AsSuccess.Result;
        var result = await _service.ApplyResourceDeletionAsync(plan, _palette);

        Assert.True(result.HasSucceeded);
        Assert.False(_tree.TryGetNode("/Packed", out _));
        Assert.False(_tree.TryGetNode("/Sprite", out _));
        Assert.False(File.Exists(Path.Combine(_directory, "Packed.xml")));
    }
}
