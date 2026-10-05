using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;

namespace ImageMagitek.Services;

public interface ICodecService
{
    ICodecFactory CodecFactory { get; }

    IEnumerable<string> GetSupportedCodecNames();
    MagitekResults LoadCodecs(string codecsPath);
    void AddOrUpdateCodec(Type codecType);
}

public sealed class XmlCodecService : ICodecService
{
    public ICodecFactory CodecFactory { get; }

    private readonly string _schemaFileName;

    public XmlCodecService(string schemaFileName, CodecFactory codecFactory)
    {
        _schemaFileName = schemaFileName;
        CodecFactory = codecFactory;
    }

    public MagitekResults LoadCodecs(string codecsPath)
    {
        var formats = new Dictionary<string, string>();
        var serializer = new XmlGraphicsFormatReader(_schemaFileName);
        var errors = new List<string>();

        foreach (var formatFileName in Directory.GetFiles(codecsPath).Where(x => x.EndsWith(".xml")).Order(StringComparer.Ordinal))
        {
            var result = serializer.LoadFromFile(formatFileName);

            result.Switch(success =>
                {
                    var name = success.Result.Name;
                    if (formats.TryAdd(name, formatFileName))
                        CodecFactory.AddOrUpdateFormat(success.Result);
                    else
                        errors.Add($"XML codec '{name}' in '{formatFileName}' duplicates '{formats[name]}' and was skipped");
                },
                fail =>
                {
                    errors.Add($"Failed to load XML codec '{formatFileName}'");
                    errors.AddRange(fail.Reasons);
                });
        }

        if (errors.Any())
            return new MagitekResults.Failed(errors);
        else
            return MagitekResults.SuccessResults;
    }

    public void AddOrUpdateCodec(Type codecType) => CodecFactory.AddOrUpdateCodec(codecType);

    public IEnumerable<string> GetSupportedCodecNames() => CodecFactory.GetRegisteredCodecNames();
}
