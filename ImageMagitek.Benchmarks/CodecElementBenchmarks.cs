using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Colors.Serialization;

namespace ImageMagitek.Benchmarks;

/// <summary>
/// Measures single-element decode, encode and read for the specialized and generalized indexed codecs.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CodecElementBenchmarks
{
    private static readonly string[] _codecFiles = ["SNES3bpp Flow.xml", "SNES4bpp Pattern.xml", "PSX4bpp.xml"];

    [Params("SNES 3bpp", "SNES 3bpp Flow", "SNES4bpp Pattern", "PSX 4bpp Flow")]
    public string CodecName { get; set; } = "";

    private IIndexedCodec _codec = null!;
    private ArrangerElement _el;
    private byte[] _encoded = [];
    private byte[,] _pixels = new byte[0, 0];

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Source-tree assets: the ImageMagitek build does not copy the pattern codec XMLs to output
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

        var size = CodecName == "PSX 4bpp Flow" ? new Size(64, 64) : new Size(8, 8);
        _codec = (IIndexedCodec)new CodecFactory(palette, formats).CreateCodec(CodecName, size)!;

        _encoded = new byte[(_codec.StorageSize + 7) / 8];
        new Random(1).NextBytes(_encoded);

        var source = new MemoryDataSource("Benchmark", _encoded.Length);
        source.Write(BitAddress.Zero, _codec.StorageSize, _encoded);

        _el = new ArrangerElement(0, 0, source, BitAddress.Zero, _codec);
        _pixels = (byte[,])_codec.DecodeElement(_el, _encoded).Clone();
    }

    [Benchmark]
    public byte[,] Decode() => _codec.DecodeElement(_el, _encoded);

    [Benchmark]
    public int Encode() => _codec.EncodeElement(_el, _pixels).Length;

    [Benchmark]
    public int ReadElement() => _codec.ReadElement(_el).Length;

    private static string ThisDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
