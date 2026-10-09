using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using Xunit;

namespace ImageMagitek.UnitTests.Fixtures;
public class CodecFixture : IDisposable
{
    private static readonly Lazy<CodecFixture> _shared = new(() => new CodecFixture());

    /// <summary>
    /// Instance shared with static MemberData providers, which run before collection fixtures are created.
    /// </summary>
    public static CodecFixture Shared => _shared.Value;

    public ICodecFactory CodecFactory { get; }
    public ICodecService CodecService { get; }

    /// <summary>
    /// Names of every loaded XML flow and pattern codec, ordered by name.
    /// </summary>
    public IReadOnlyList<string> XmlCodecNames { get; }

    public CodecFixture()
    {
        var paletteService = new PaletteService(new ColorFactory());
        var palette = paletteService.ReadJsonPalette(Path.Combine(AppContext.BaseDirectory, "_palettes", "DefaultRgba32.json"))!;

        CodecFactory = new CodecFactory(palette, []);
        CodecService = new XmlCodecService(Path.Combine(AppContext.BaseDirectory, "_schemas", "CodecSchema.xsd"), (CodecFactory)CodecFactory);

        var failures = CodecService.LoadCodecs(TestPaths.CodecsPath);
        if (failures.Count > 0)
            throw new InvalidOperationException($"Failed to load codecs from '{TestPaths.CodecsPath}':\n{string.Join("\n", failures.Select(x => $"{x.FileName}: {x.Message}"))}");

        XmlCodecNames = CodecFactory.GetRegisteredCodecNames()
            .Where(x => CodecFactory.CreateCodec(x) is IndexedFlowGraphicsCodec or IndexedPatternGraphicsCodec)
            .ToList();

        DirectCodecNames = CodecFactory.GetRegisteredCodecNames()
            .Where(x => CodecFactory.CreateCodec(x) is IDirectCodec)
            .ToList();
    }

    /// <summary>
    /// Names of every built-in direct-color codec, ordered by name.
    /// </summary>
    public IReadOnlyList<string> DirectCodecNames { get; }

    public void Dispose()
    {
    }
}

[CollectionDefinition("Codec")]
public class CodecCollectionFixture : ICollectionFixture<CodecFixture>
{
}
