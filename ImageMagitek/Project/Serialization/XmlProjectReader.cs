#nullable disable

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Xml.Schema;
using System.Collections.Generic;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Compression;
using ImageMagitek.Tilemaps;
using System.Diagnostics.CodeAnalysis;

namespace ImageMagitek.Project.Serialization;

public sealed class XmlProjectReader : IProjectReader
{
    private readonly XmlSchemaSet _resourceSchema;

    private readonly ICodecFactory _codecFactory;
    private readonly IColorFactory _colorFactory;
    private readonly ICompressorFactory _compressorFactory;
    private readonly IMapFormatFactory _mapFormatFactory;
    private readonly List<IProjectResource> _globalResources;
    private readonly Palette _globalDefaultPalette;
    private readonly ResourceKindRegistry _kinds;

    private List<string> _errors = new();
    private string _baseDirectory;

    public XmlProjectReader(XmlSchemaSet resourceSchema,
        ICodecFactory codecFactory, IColorFactory colorFactory, IEnumerable<IProjectResource> globalResources, ICompressorFactory compressorFactory = null,
        IMapFormatFactory mapFormatFactory = null, ResourceKindRegistry kinds = null)
    {
        _resourceSchema = resourceSchema;
        _kinds = kinds ?? ResourceKindRegistry.BuiltIn;
        _codecFactory = codecFactory;
        _colorFactory = colorFactory;
        _compressorFactory = compressorFactory ?? new CompressorFactory();
        _mapFormatFactory = mapFormatFactory ?? new MapFormatFactory();
        _globalResources = globalResources.ToList();
        _globalDefaultPalette = _globalResources.OfType<Palette>().First();
    }

    public MagitekResults<ProjectTree> ReadProject(string projectFileName)
    {
        if (string.IsNullOrWhiteSpace(projectFileName))
            throw new ArgumentException($"{nameof(ReadProject)} cannot have a null or empty value for '{nameof(projectFileName)}'");

        _errors = new();

        var fullProjectFileName = Path.GetFullPath(projectFileName);
        var projectDirectory = Path.GetDirectoryName(fullProjectFileName);
        _baseDirectory = projectDirectory;

        if (!TryLoadXml(projectFileName, out var projectDoc))
            return new MagitekResults<ProjectTree>.Failed(_errors);

        if (projectDoc.Root.Name.LocalName != "project")
        {
            _errors.Add($"'{projectFileName}' was expected to be a project file");
            return new MagitekResults<ProjectTree>.Failed(_errors);
        }

        if (!TryReadVersion(projectDoc.Root, projectFileName, out var version) || !TryValidate(projectDoc, projectFileName))
            return new MagitekResults<ProjectTree>.Failed(_errors);

        var projectModel = DeserializeProject(projectDoc.Root, Path.GetFileNameWithoutExtension(projectFileName), version);

        var builder = new ProjectTreeBuilder(_codecFactory, _colorFactory, _globalDefaultPalette, _globalResources, _compressorFactory, _mapFormatFactory);

        if (!string.IsNullOrWhiteSpace(projectModel.Root))
            _baseDirectory = Path.GetFullPath(Path.Combine(projectDirectory, projectModel.Root));

        builder.AddProject(projectModel, _baseDirectory, fullProjectFileName);

        var journalPath = Path.Combine(projectDirectory, "_transaction.json");
        if (File.Exists(journalPath))
        {
            _errors.Add($"Project directory contains an incomplete transaction journal '{journalPath}'. Recovery must be performed before the project can be loaded.");
            return new MagitekResults<ProjectTree>.Failed(_errors);
        }

        var resourceFileNames = new List<string>();
        ScanDirectory(_baseDirectory, builder, resourceFileNames);

        List<(ResourceModel model, ResourceKind kind, string pathKey, string fileLocation)> resourceModels = new();

        foreach (var resourceFileName in resourceFileNames)
        {
            if (!TryLoadXml(resourceFileName, out var doc))
                return new MagitekResults<ProjectTree>.Failed(_errors);

            if (_kinds.FindByElement(doc.Root.Name.LocalName) is not { } kind)
                continue;

            if (!TryValidate(doc, resourceFileName) || !TryReadResource(kind, doc, resourceFileName, out var resourceModel))
                return new MagitekResults<ProjectTree>.Failed(_errors);

            var pathKey = LocateParentPathKey(resourceFileName);
            if (string.IsNullOrWhiteSpace(pathKey) || pathKey == ".")
                pathKey = string.Empty;
            resourceModels.Add((resourceModel, kind, pathKey, resourceFileName));
        }

        foreach (var kind in _kinds.Kinds)
        {
            var pending = resourceModels.Where(x => x.kind == kind).ToList();

            if (kind.BuildsInPasses)
            {
                BuildInPasses(builder, kind, pending);
                continue;
            }

            foreach (var (model, _, pathKey, fileLocation) in pending)
            {
                var result = kind.Build(builder, model, pathKey, fileLocation);
                if (result.HasFailed)
                    _errors.AddRange(result.AsError.Reasons);
            }
        }

        if (_errors.Count > 0)
            return new MagitekResults<ProjectTree>.Failed(_errors);

        return new MagitekResults<ProjectTree>.Success(builder.Tree);
    }

    /// <summary>
    /// Builds a kind's models in passes, so a resource built over another of the same kind is built after it
    /// </summary>
    private void BuildInPasses(ProjectTreeBuilder builder, ResourceKind kind,
        List<(ResourceModel model, ResourceKind kind, string pathKey, string fileLocation)> pending)
    {
        while (pending.Count > 0)
        {
            var failures = new List<((ResourceModel model, ResourceKind kind, string pathKey, string fileLocation) item, List<string> reasons)>();

            foreach (var item in pending)
            {
                var result = kind.Build(builder, item.model, item.pathKey, item.fileLocation);
                if (result.HasFailed)
                    failures.Add((item, result.AsError.Reasons));
            }

            if (failures.Count == pending.Count)
            {
                _errors.AddRange(failures.SelectMany(x => x.reasons));
                return;
            }

            pending = failures.Select(x => x.item).ToList();
        }
    }

    private void ScanDirectory(string directory, ProjectTreeBuilder builder, List<string> xmlFileNames)
    {
        foreach (var fileName in Directory.EnumerateFiles(directory, "*.xml"))
        {
            if (!IsSkippedName(Path.GetFileName(fileName)))
                xmlFileNames.Add(fileName);
        }

        var relativePath = Path.GetRelativePath(_baseDirectory, directory);
        var parentKey = relativePath == "." ? "" : relativePath;

        foreach (var directoryName in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(directoryName);
            if (IsSkippedName(name))
                continue;

            builder.AddFolder(new ResourceFolderModel() { Name = name }, parentKey, Path.GetFullPath(directoryName));
            ScanDirectory(directoryName, builder, xmlFileNames);
        }
    }

    /// <summary>
    /// Finds the first <c>*.xml</c> file under a directory that a project load there would read or fail on, or a project file.
    /// </summary>
    public static string FindLoadableXml(string directory, ResourceKindRegistry kinds)
    {
        foreach (var fileName in Directory.EnumerateFiles(directory, "*.xml"))
        {
            if (IsSkippedName(Path.GetFileName(fileName)))
                continue;

            try
            {
                var root = XDocument.Parse(File.ReadAllText(fileName)).Root.Name.LocalName;
                if (kinds.FindByElement(root) is not null || root == "project")
                    return fileName;
            }
            catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
            {
                return fileName;
            }
        }

        foreach (var directoryName in Directory.EnumerateDirectories(directory))
        {
            if (!IsSkippedName(Path.GetFileName(directoryName)) && FindLoadableXml(directoryName, kinds) is { } found)
                return found;
        }

        return null;
    }

    private static bool IsSkippedName(string name) => name.StartsWith('.');

    private bool TryLoadXml(string xmlFileName, [MaybeNullWhen(false)] out XDocument doc)
    {
        doc = default;
        if (!File.Exists(xmlFileName))
        {
            _errors.Add($"File '{xmlFileName}' does not exist");
            return false;
        }

        try
        {
            doc = XDocument.Parse(File.ReadAllText(xmlFileName), LoadOptions.SetLineInfo);
            return true;
        }
        catch (XmlException ex)
        {
            _errors.Add($"'{xmlFileName}' line {ex.LineNumber}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _errors.Add($"An exception occurred while reading '{xmlFileName}': {ex.Message}");
        }

        return false;
    }

    private bool TryValidate(XDocument doc, string xmlFileName)
    {
        if (_resourceSchema is null)
            return true;

        var errorCount = _errors.Count;
        doc.Validate(_resourceSchema, (o, e) =>
        {
            var line = o as IXmlLineInfo;
            _errors.Add($"'{xmlFileName}' line {line?.LineNumber.ToString() ?? "Unknown"}: {e.Message}");
        });

        return _errors.Count == errorCount;
    }

    private bool TryReadVersion(XElement projectElement, string projectFileName, out Version version)
    {
        var fileName = Path.GetFileName(projectFileName);
        var text = projectElement.Attribute("version")?.Value;
        version = null;

        if (text is null)
            _errors.Add($"'{fileName}' has no project format version");
        else if (!ProjectFormat.TryParse(text, out version))
            _errors.Add($"'{fileName}' has project format version '{text}', which is not a major.minor version");
        else if (version < ProjectFormat.Oldest)
            _errors.Add($"'{fileName}' uses project format {text}; this version of TileShop reads format {ProjectFormat.Oldest} and newer.");
        else if (version > ProjectFormat.Current)
            _errors.Add($"'{fileName}' uses project format {text}; this version of TileShop reads format {ProjectFormat.Current} and older.");
        else
            return true;

        return false;
    }

    private bool TryReadResource(ResourceKind kind, XDocument doc, string xmlFileName, [MaybeNullWhen(false)] out ResourceModel model)
    {
        try
        {
            model = kind.Read(doc.Root, new ResourceReadContext(xmlFileName, _baseDirectory, _colorFactory, _globalDefaultPalette, _errors));
            return model is not null;
        }
        catch (Exception ex)
        {
            _errors.Add($"An exception occurred while reading '{xmlFileName}': {ex.Message}");
            model = default;
            return false;
        }
    }

    private string LocateParentPathKey(string fullFileName)
    {
        var path = Directory.GetParent(fullFileName);
        var relativePath = Path.GetRelativePath(_baseDirectory, path.FullName);
        return string.Join('/', relativePath.Split('\\'));
    }

    private static ImageProjectModel DeserializeProject(XElement element, string resourceName, Version version) =>
        new()
        {
            Name = resourceName,
            Root = element.Attribute("root")?.Value ?? string.Empty,
            // 0.9 holds the same content as 1.0, so recording it as 1.0 keeps an unchanged save from rewriting it
            Version = version < ProjectFormat.Minimum ? ProjectFormat.Minimum : version
        };
}
