using System;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using Xunit;

namespace ImageMagitek.UnitTests.Fixtures;
public class CodecFixture : IDisposable
{
    public ICodecFactory CodecFactory { get; }
    public ICodecService CodecService { get; }

    public CodecFixture()
    {
        var paletteService = new PaletteService(new ColorFactory());
        var palette = paletteService.ReadJsonPalette(@"_palettes/DefaultRgba32.json")!;

        CodecFactory = new CodecFactory(palette, []);
        CodecService = new XmlCodecService(@"_schemas/CodecSchema.xsd", (CodecFactory)CodecFactory);
        CodecService.LoadCodecs(@"_codecs");
    }

    public void Dispose()
    {
    }
}

[CollectionDefinition("Codec")]
public class CodecCollectionFixture : ICollectionFixture<CodecFixture>
{
}