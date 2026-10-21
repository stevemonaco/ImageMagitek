using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using CommunityToolkit.Diagnostics;

namespace ImageMagitek.Tables;

/// <summary>
/// Reads charmap files validated against <c>CharmapSchema.xsd</c>
/// </summary>
public sealed class XmlCharmapReader
{
    private readonly XmlSchemaSet _schemas;

    public XmlCharmapReader(string schemaFileName)
    {
        _schemas = TableXml.LoadSchema(schemaFileName);
    }

    public MagitekResults<Charmap> LoadFromFile(string fileName)
    {
        Guard.IsNotNullOrWhiteSpace(fileName);

        var loadResult = TableXml.LoadValidated(fileName, _schemas, "Charmap");
        if (loadResult.HasFailed)
            return new MagitekResults<Charmap>.Failed(loadResult.AsError.Reasons);

        var root = loadResult.AsSuccess.Result.Root!;
        var mappings = new Dictionary<byte, string>();
        var errors = new List<string>();

        foreach (var element in root.Elements())
        {
            var start = byte.Parse(element.Attribute("byte")!.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var text = element.Attribute("text")!.Value;

            if (element.Name.LocalName == "map")
            {
                mappings[start] = text;
                continue;
            }

            var runes = text.EnumerateRunes().Select(x => x.ToString()).ToList();

            if (start + runes.Count > 0x100)
            {
                errors.Add($"'{fileName}' line {TableXml.LineOf(element)}: run at {start:X2} of {runes.Count} characters passes byte FF");
                continue;
            }

            for (var i = 0; i < runes.Count; i++)
                mappings[(byte)(start + i)] = runes[i];
        }

        if (errors.Count > 0)
            return new MagitekResults<Charmap>.Failed(errors);

        var terminator = byte.Parse(root.Attribute("terminator")!.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new MagitekResults<Charmap>.Success(new Charmap(root.Attribute("name")!.Value, terminator, mappings));
    }
}

/// <summary>
/// Shared XML loading for table definitions, charmaps and manifests
/// </summary>
internal static class TableXml
{
    public static XmlSchemaSet LoadSchema(string schemaFileName)
    {
        Guard.IsNotNullOrWhiteSpace(schemaFileName);

        if (!File.Exists(schemaFileName))
            throw new ArgumentException($"Schema '{schemaFileName}' cannot be found");

        var schemas = new XmlSchemaSet();
        using var stream = File.OpenRead(schemaFileName);
        schemas.Add("", XmlReader.Create(stream));
        return schemas;
    }

    public static MagitekResults<XDocument> LoadValidated(string fileName, XmlSchemaSet schemas, string kind)
    {
        if (!File.Exists(fileName))
            return new MagitekResults<XDocument>.Failed($"{kind} '{fileName}' does not exist");

        XDocument doc;
        try
        {
            using var stream = File.OpenRead(fileName);
            doc = XDocument.Load(stream, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            return new MagitekResults<XDocument>.Failed($"'{fileName}' line {ex.LineNumber}: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MagitekResults<XDocument>.Failed($"{kind} '{fileName}' could not be read: {ex.Message}");
        }

        var errors = new List<string>();
        doc.Validate(schemas, (o, e) => errors.Add($"'{fileName}' line {(o as IXmlLineInfo)?.LineNumber.ToString() ?? "unknown"}: {e.Message}"));

        if (errors.Count > 0)
            return new MagitekResults<XDocument>.Failed(errors);

        return new MagitekResults<XDocument>.Success(doc);
    }

    public static int LineOf(XObject node) => ((IXmlLineInfo)node).LineNumber;
}
