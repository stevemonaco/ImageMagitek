using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Colors.Serialization;
using ImageMagitek.PluginSample;

namespace ImageMagitek.Benchmarks;

/// <summary>
/// Measures single-element decode, encode and read for the sample SNES 3bpp codec, the generalized indexed codecs and a direct-color codec.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CodecElementBenchmarks
{
    private static readonly string[] _codecFiles = ["SNES3bpp Flow.xml", "SNES4bpp Pattern.xml", "PSX4bpp.xml"];

    [Params("SNES 3bpp Plugin", "SNES 3bpp Flow", "SNES4bpp Pattern", "PSX 4bpp Flow", "PSX 16bpp")]
    public string CodecName { get; set; } = "";

    private IIndexedCodec? _indexedCodec;
    private IDirectCodec? _directCodec;
    private ArrangerElement _el;
    private byte[] _encoded = [];
    private byte[,] _pixels = new byte[0, 0];
    private ColorRgba32[,] _colors = new ColorRgba32[0, 0];

    [GlobalSetup]
    public void GlobalSetup()
    {
        var root = Path.GetFullPath(Path.Combine(ThisDir(), "..", "ImageMagitek"));
        var reader = new XmlGraphicsFormatReader(Path.Combine(root, "_schemas", "CodecSchema.xsd"));

        var formats = new Dictionary<string, IGraphicsFormat>();
        foreach (var file in _codecFiles)
        {
            var format = reader.LoadFromFile(Path.Combine(root, "_codecs", file)).AsSuccess.Result;
            formats[format.Name] = format;
        }

        var palContents = File.ReadAllText(Path.Combine(root, "_palettes", "DefaultRgba32.json"));
        var palette = PaletteJsonSerializer.DeserializePalette(palContents, new ColorFactory())!;

        var factory = new CodecFactory(palette, formats);
        factory.AddCodec(typeof(Snes3BppCodec));

        var size = CodecName is "PSX 4bpp Flow" or "PSX 16bpp" ? new Size(64, 64) : new Size(8, 8);
        var codec = factory.CreateCodec(CodecName, size)!;
        _indexedCodec = codec as IIndexedCodec;
        _directCodec = codec as IDirectCodec;

        _encoded = new byte[(codec.StorageSize + 7) / 8];
        new Random(1).NextBytes(_encoded);

        var source = new MemoryDataSource("Benchmark", _encoded.Length);
        source.Write(BitAddress.Zero, codec.StorageSize, _encoded);

        _el = new ArrangerElement(0, 0, source, BitAddress.Zero, codec);

        if (_indexedCodec is not null)
            _pixels = (byte[,])_indexedCodec.DecodeElement(_el, _encoded).Clone();
        else
            _colors = (ColorRgba32[,])_directCodec!.DecodeElement(_el, _encoded).Clone();
    }

    [Benchmark]
    public object Decode() => _indexedCodec is not null
        ? _indexedCodec.DecodeElement(_el, _encoded)
        : _directCodec!.DecodeElement(_el, _encoded);

    [Benchmark]
    public int Encode() => _indexedCodec is not null
        ? _indexedCodec.EncodeElement(_el, _pixels).Length
        : _directCodec!.EncodeElement(_el, _colors).Length;

    [Benchmark]
    public int ReadElement() => _indexedCodec is not null
        ? _indexedCodec.ReadElement(_el).Length
        : _directCodec!.ReadElement(_el).Length;

    private static string ThisDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
