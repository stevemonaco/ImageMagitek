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
    IReadOnlyList<CodecFileFailure> LoadCodecs(string codecsPath);
    MagitekResult<string> AddCodecPlugin(Type pluginType);
}

/// <summary>
/// An XML codec file that failed to load or register, with its reasons separated by new lines
/// </summary>
public sealed record CodecFileFailure(string FileName, string Message);

public sealed class XmlCodecService : ICodecService
{
    public ICodecFactory CodecFactory { get; }

    private readonly string _schemaFileName;

    public XmlCodecService(string schemaFileName, CodecFactory codecFactory)
    {
        _schemaFileName = schemaFileName;
        CodecFactory = codecFactory;
    }


    /// <summary>
    /// Loads each XML codec file in ordinal name order and registers it with the codec factory
    /// </summary>
    /// <returns>One failure for each file that did not load or register</returns>
    public IReadOnlyList<CodecFileFailure> LoadCodecs(string codecsPath)
    {
        var fileNamesByCodec = new Dictionary<string, string>();
        var serializer = new XmlGraphicsFormatReader(_schemaFileName);
        var failures = new List<CodecFileFailure>();

        foreach (var formatFileName in Directory.GetFiles(codecsPath).Where(x => x.EndsWith(".xml")).Order(StringComparer.Ordinal))
        {
            var result = serializer.LoadFromFile(formatFileName);

            result.Switch(success =>
                {
                    var name = success.Result.Name;
                    var added = CodecFactory.AddFormat(success.Result);

                    if (added.HasSucceeded)
                        fileNamesByCodec[name] = formatFileName;
                    else if (fileNamesByCodec.TryGetValue(name, out var firstFileName))
                        failures.Add(new(formatFileName, $"XML codec '{name}' in '{formatFileName}' duplicates '{firstFileName}' and was skipped"));
                    else
                        failures.Add(new(formatFileName, added.AsError.Reason));
                },
                fail => failures.Add(new(formatFileName, string.Join(Environment.NewLine, fail.Reasons))));
        }

        return failures;
    }

    public MagitekResult<string> AddCodecPlugin(Type pluginType) => CodecFactory.AddCodecPlugin(pluginType);

    public IEnumerable<string> GetSupportedCodecNames() => CodecFactory.GetRegisteredCodecNames();
}
