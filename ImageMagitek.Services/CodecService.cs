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
    MagitekResults LoadXmlCodecs(string codecsPath);
    void AddOrUpdateCodec(Type codecType);
}

public class CodecService : ICodecService
{
    public ICodecFactory CodecFactory { get; private set; } = new CodecFactory(new());

    private readonly string _schemaFileName;

    public CodecService(string schemaFileName)
    {
        _schemaFileName = schemaFileName;
    }

    public MagitekResults LoadXmlCodecs(string codecsPath)
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
                        formats.Add(success.Result.Name, success.Result);
                    }
                },
                fail =>
                {
                    errors.Add($"Failed to load XML codec '{formatFileName}'");
                    errors.AddRange(fail.Reasons);
                });
        }

        CodecFactory = new CodecFactory(formats);

        if (errors.Any())
            return new MagitekResults.Failed(errors);
        else
            return MagitekResults.SuccessResults;
    }

    public void AddOrUpdateCodec(Type codecType) => CodecFactory.AddOrUpdateCodec(codecType);

    public IEnumerable<string> GetSupportedCodecNames() => CodecFactory.GetRegisteredCodecNames();
}
