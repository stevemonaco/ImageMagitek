using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using CommunityToolkit.Diagnostics;

namespace ImageMagitek.Tilemaps;

/// <summary>
/// Reads XML map format files validated against the map format schema
/// </summary>
public sealed class XmlMapFormatReader
{
    private readonly XmlSchemaSet _schemas = new();

    public XmlMapFormatReader(string schemaFileName)
    {
        Guard.IsNotNullOrWhiteSpace(schemaFileName);

        if (!File.Exists(schemaFileName))
            throw new ArgumentException($"{nameof(schemaFileName)} cannot be found");

        using var schemaStream = File.OpenRead(schemaFileName);
        _schemas.Add("", XmlReader.Create(schemaStream));
    }

    public MagitekResults<MapFormat> LoadFromFile(string fileName)
    {
        if (!File.Exists(fileName))
            return new MagitekResults<MapFormat>.Failed($"Map format file '{fileName}' does not exist");

        XDocument doc;
        try
        {
            using var stream = File.OpenRead(fileName);
            doc = XDocument.Load(stream, LoadOptions.SetLineInfo);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return new MagitekResults<MapFormat>.Failed($"Map format '{fileName}': {ex.Message}");
        }

        var validationErrors = new List<string>();
        doc.Validate(_schemas, (o, e) => validationErrors.Add(e.Message));

        if (validationErrors.Count > 0)
        {
            validationErrors.Insert(0, $"Map format '{fileName}' failed to be validated");
            return new MagitekResults<MapFormat>.Failed(validationErrors);
        }

        var root = doc.Root!;

        try
        {
            var format = new MapFormat(
                root.Attribute("name")!.Value,
                int.Parse(root.Attribute("entrybits")!.Value),
                root.Attribute("endian")?.Value == "big" ? Endian.Big : Endian.Little,
                ReadField(root.Element("tileindex"))!.Value,
                ReadField(root.Element("palette")),
                ReadFlag(root.Element("hflip")),
                ReadFlag(root.Element("vflip")),
                ReadFlag(root.Element("priority")));

            return new MagitekResults<MapFormat>.Success(format);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            return new MagitekResults<MapFormat>.Failed($"Map format '{fileName}': {ex.Message}");
        }
    }

    private static MapField? ReadField(XElement? element) => element is null
        ? null
        : new MapField(int.Parse(element.Attribute("lowbit")?.Value ?? "0"), int.Parse(element.Attribute("bits")!.Value));

    private static MapField? ReadFlag(XElement? element) => element is null
        ? null
        : new MapField(int.Parse(element.Attribute("bit")!.Value), 1);
}
