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
        var formats = new Dictionary<string, IGraphicsFormat>();
        var serializer = new XmlGraphicsFormatReader(_schemaFileName);
        var errors = new List<string>();

        foreach (var formatFileName in Directory.GetFiles(codecsPath).Where(x => x.EndsWith(".xml")))
        {
            var result = serializer.LoadFromFile(formatFileName);

            result.Switch(success =>
                {
                    if (formats.ContainsKey(success.Result.Name))
                    {
                        errors.Add($"Failed to load XML codec '{formatFileName}'");
                        errors.AddRange(new[] { $"XML codec with name '{formatFileName}' already exists"});
                    }
                    else
                    {
                        CodecFactory.AddOrUpdateFormat(success.Result);
                    }
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
