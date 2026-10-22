using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Tables;

/// <summary>
/// One generated resource and the record it came from (LIB-TABLES-131, -135)
/// </summary>
/// <param name="Path">The record path (LIB-TABLES-160), absent in manifests written without paths</param>
/// <param name="Hash">Lowercase hexadecimal SHA-256 of the resource file as written</param>
public sealed record TableManifestResource(string Key, string Type, string Output, string Record, long Offset, int? Entries = null, int? Index = null,
    string? Path = null, string? Hash = null);

/// <summary>
/// One generation run of a definition into a project folder (LIB-TABLES-130)
/// </summary>
/// <param name="Project">The project file's path relative to the manifest's directory, '/'-separated</param>
/// <param name="Folder">The target folder's tree path key, '/' for the project root</param>
/// <param name="Definition">The definition's path relative to the manifest's directory, '/'-separated</param>
public sealed record TableManifestGeneration(string Project, string Folder, string DataFile, string Definition, IReadOnlyList<TableManifestResource> Resources);

/// <summary>
/// The open manifest format listing generated resources by generation (LIB-TABLES-130 to -134)
/// </summary>
public sealed record TableManifest(IReadOnlyList<TableManifestGeneration> Generations)
{
    public const string Version = "1.0";

    public static TableManifest Empty { get; } = new([]);

    /// <summary>
    /// Replaces the generation with the same project and folder, or appends it, keeping every other generation
    /// </summary>
    public TableManifest WithGeneration(TableManifestGeneration generation)
    {
        var generations = Generations.ToList();
        var index = generations.FindIndex(x => IsSameGeneration(x, generation.Project, generation.Folder));

        if (index >= 0)
            generations[index] = generation;
        else
            generations.Add(generation);

        return new TableManifest(generations);
    }

    /// <summary>
    /// The generation with this project and folder, compared ignoring case (LIB-TABLES-133)
    /// </summary>
    public TableManifestGeneration? FindGeneration(string project, string folder) =>
        Generations.FirstOrDefault(x => IsSameGeneration(x, project, folder));

    private static bool IsSameGeneration(TableManifestGeneration generation, string project, string folder) =>
        string.Equals(generation.Project, project, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(generation.Folder, folder, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The lowercase hexadecimal SHA-256 of <paramref name="contents"/> as UTF-8, as resource files are written
    /// </summary>
    public static string HashContents(string contents) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contents)));

    public static string HashFile(string fileName) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(fileName)));
}

/// <summary>
/// Reads manifests validated against <c>TableManifestSchema.xsd</c>, whoever wrote them
/// </summary>
public sealed class XmlTableManifestReader
{
    private readonly XmlSchemaSet _schemas;

    public XmlTableManifestReader(string schemaFileName)
    {
        _schemas = TableXml.LoadSchema(schemaFileName);
    }

    public MagitekResults<TableManifest> LoadFromFile(string fileName)
    {
        var loadResult = TableXml.LoadValidated(fileName, _schemas, "Manifest");
        if (loadResult.HasFailed)
            return new MagitekResults<TableManifest>.Failed(loadResult.AsError.Reasons);

        try
        {
            return new MagitekResults<TableManifest>.Success(new TableManifest(ReadGenerations(loadResult.AsSuccess.Result.Root!)));
        }
        catch (OverflowException)
        {
            return new MagitekResults<TableManifest>.Failed([$"Manifest '{fileName}' has a resource offset, entries or index that is too large"]);
        }
    }

    private static List<TableManifestGeneration> ReadGenerations(XElement root) =>
        root.Elements("generation").Select(generation =>
            new TableManifestGeneration(
                generation.Attribute("project")!.Value,
                generation.Attribute("folder")!.Value,
                generation.Attribute("datafile")!.Value,
                generation.Attribute("definition")!.Value,
                generation.Elements("resource").Select(resource => new TableManifestResource(
                    resource.Attribute("key")!.Value,
                    resource.Attribute("type")!.Value,
                    resource.Attribute("output")!.Value,
                    resource.Attribute("record")!.Value,
                    long.Parse(resource.Attribute("offset")!.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    resource.Attribute("entries") is { } entries ? int.Parse(entries.Value, CultureInfo.InvariantCulture) : null,
                    resource.Attribute("index") is { } index ? int.Parse(index.Value, CultureInfo.InvariantCulture) : null,
                    resource.Attribute("path")?.Value,
                    resource.Attribute("hash")?.Value.ToLowerInvariant())).ToList()))
            .ToList();
}

/// <summary>
/// Writes manifests as UTF-8 XML with tab indentation and no timestamps
/// </summary>
public static class XmlTableManifestWriter
{
    public static string Serialize(TableManifest manifest)
    {
        var root = new XElement("tablemanifest", new XAttribute("version", TableManifest.Version));

        foreach (var generation in manifest.Generations)
        {
            var element = new XElement("generation",
                new XAttribute("project", generation.Project),
                new XAttribute("folder", generation.Folder),
                new XAttribute("datafile", generation.DataFile),
                new XAttribute("definition", generation.Definition));

            foreach (var resource in generation.Resources)
            {
                var resourceElement = new XElement("resource",
                    new XAttribute("key", resource.Key),
                    new XAttribute("type", resource.Type),
                    new XAttribute("output", resource.Output),
                    new XAttribute("record", resource.Record),
                    new XAttribute("offset", resource.Offset.ToString("X", CultureInfo.InvariantCulture)));

                if (resource.Entries is { } entries)
                    resourceElement.Add(new XAttribute("entries", entries));

                if (resource.Index is { } index)
                    resourceElement.Add(new XAttribute("index", index));

                if (resource.Path is { } path)
                    resourceElement.Add(new XAttribute("path", path));

                if (resource.Hash is { } hash)
                    resourceElement.Add(new XAttribute("hash", hash));

                element.Add(resourceElement);
            }

            root.Add(element);
        }

        using var writer = new Utf8StringWriter();
        using (var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true, IndentChars = "\t" }))
            root.Save(xmlWriter);

        return writer.ToString();
    }
}
