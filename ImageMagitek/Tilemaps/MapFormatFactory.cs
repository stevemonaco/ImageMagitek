using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ImageMagitek.Tilemaps;

/// <summary>
/// An XML map format file that failed to load or register, with its reasons separated by new lines
/// </summary>
public sealed record MapFormatFileFailure(string FileName, string Message);

/// <summary>
/// A case-insensitive registry of map formats by name
/// </summary>
public sealed class MapFormatFactory : IMapFormatFactory
{
    private readonly Dictionary<string, MapFormat> _formats = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MapFormat> _formatList = [];

    public IReadOnlyList<MapFormat> Formats => _formatList;

    public MagitekResult AddFormat(MapFormat format)
    {
        if (!_formats.TryAdd(format.Name, format))
            return new MagitekResult.Failed($"Map format '{format.Name}' is already registered");

        _formatList.Add(format);
        return MagitekResult.SuccessResult;
    }

    public MapFormat? Find(string name) => _formats.GetValueOrDefault(name);

    /// <summary>
    /// Loads each XML map format file in <paramref name="directory"/> in ordinal name order and registers it
    /// </summary>
    /// <returns>One failure for each file that did not load or register</returns>
    public IReadOnlyList<MapFormatFileFailure> LoadFormats(string directory, string schemaFileName)
    {
        var reader = new XmlMapFormatReader(schemaFileName);
        var fileNamesByFormat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var failures = new List<MapFormatFileFailure>();

        foreach (var fileName in Directory.GetFiles(directory, "*.xml").Order(StringComparer.Ordinal))
        {
            reader.LoadFromFile(fileName).Switch(
                success =>
                {
                    var format = success.Result;
                    if (AddFormat(format).HasSucceeded)
                        fileNamesByFormat[format.Name] = fileName;
                    else
                        failures.Add(new(fileName, $"Map format '{format.Name}' in '{fileName}' duplicates '{fileNamesByFormat.GetValueOrDefault(format.Name)}' and was skipped"));
                },
                fail => failures.Add(new(fileName, string.Join(Environment.NewLine, fail.Reasons))));
        }

        return failures;
    }
}
