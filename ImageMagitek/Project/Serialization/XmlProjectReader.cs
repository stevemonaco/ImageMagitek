#nullable disable

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Drawing;
using System.Xml.Schema;
using System.Collections.Generic;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Utility.Parsing;
using System.Diagnostics.CodeAnalysis;

namespace ImageMagitek.Project.Serialization;

public sealed class XmlProjectReader : IProjectReader
{
    private readonly XmlSchemaSet _resourceSchema;

    private readonly ICodecFactory _codecFactory;
    private readonly IColorFactory _colorFactory;
    private readonly List<IProjectResource> _globalResources;
    private readonly Palette _globalDefaultPalette;

    private List<string> _errors = new();
    private string _baseDirectory;

    public XmlProjectReader(XmlSchemaSet resourceSchema,
        ICodecFactory codecFactory, IColorFactory colorFactory, IEnumerable<IProjectResource> globalResources)
    {
        _resourceSchema = resourceSchema;
        _codecFactory = codecFactory;
        _colorFactory = colorFactory;
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

        if (!TryReadVersion(projectDoc.Root, projectFileName) || !TryValidate(projectDoc, projectFileName))
            return new MagitekResults<ProjectTree>.Failed(_errors);

        var projectModel = DeserializeProject(projectDoc.Root, Path.GetFileNameWithoutExtension(projectFileName));

        var builder = new ProjectTreeBuilder(_codecFactory, _colorFactory, _globalDefaultPalette, _globalResources);

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

        List<(ResourceModel model, string pathKey, string fileLocation)> resourceModels = new();

        foreach (var resourceFileName in resourceFileNames)
        {
            if (!TryLoadXml(resourceFileName, out var doc))
                return new MagitekResults<ProjectTree>.Failed(_errors);

            if (!IsResourceRoot(doc.Root.Name.LocalName))
                continue;

            if (!TryValidate(doc, resourceFileName) || !TryDeserializeResource(doc, resourceFileName, out var resourceModel))
                return new MagitekResults<ProjectTree>.Failed(_errors);

            var pathKey = LocateParentPathKey(resourceFileName);
            if (string.IsNullOrWhiteSpace(pathKey) || pathKey == ".")
                pathKey = string.Empty;
            resourceModels.Add((resourceModel, pathKey, resourceFileName));
        }

        foreach (var (model, pathKey, fileLocation) in resourceModels.Where(x => x.model is DataFileModel))
        {
            var result = builder.AddDataFile((DataFileModel)model, pathKey, fileLocation);
            if (result.HasFailed)
            {
                _errors.Add($"{result.AsError.Reason}");
            }
        }

        foreach (var (model, pathKey, fileLocation) in resourceModels.Where(x => x.model is PaletteModel))
        {
            var result = builder.AddPalette((PaletteModel)model, pathKey, fileLocation);
            if (result.HasFailed)
            {
                _errors.Add($"{result.AsError.Reason}");
            }
        }

        foreach (var (model, pathKey, fileLocation) in resourceModels.Where(x => x.model is ScatteredArrangerModel))
        {
            var result = builder.AddScatteredArranger((ScatteredArrangerModel)model, pathKey, fileLocation);
            if (result.HasFailed)
            {
                _errors.AddRange(result.AsError.Reasons);
            }
        }

        if (_errors.Count > 0)
            return new MagitekResults<ProjectTree>.Failed(_errors);

        return new MagitekResults<ProjectTree>.Success(builder.Tree);
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
    public static string FindLoadableXml(string directory)
    {
        foreach (var fileName in Directory.EnumerateFiles(directory, "*.xml"))
        {
            if (IsSkippedName(Path.GetFileName(fileName)))
                continue;

            try
            {
                var root = XDocument.Parse(File.ReadAllText(fileName)).Root.Name.LocalName;
                if (IsResourceRoot(root) || root == "project")
                    return fileName;
            }
            catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
            {
                return fileName;
            }
        }

        foreach (var directoryName in Directory.EnumerateDirectories(directory))
        {
            if (!IsSkippedName(Path.GetFileName(directoryName)) && FindLoadableXml(directoryName) is { } found)
                return found;
        }

        return null;
    }

    private static bool IsSkippedName(string name) => name.StartsWith('.');

    private static bool IsResourceRoot(string root) => root is "datafile" or "palette" or "arranger";

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

    private bool TryReadVersion(XElement projectElement, string projectFileName)
    {
        var fileName = Path.GetFileName(projectFileName);
        var text = projectElement.Attribute("version")?.Value;

        if (text is null)
            _errors.Add($"'{fileName}' has no project format version");
        else if (!ProjectFormat.TryParse(text, out var version))
            _errors.Add($"'{fileName}' has project format version '{text}', which is not a major.minor version");
        else if (version < ProjectFormat.Oldest)
            _errors.Add($"'{fileName}' uses project format {text}; this version of TileShop reads format {ProjectFormat.Oldest} and newer.");
        else if (version > ProjectFormat.Current)
            _errors.Add($"'{fileName}' uses project format {text}; this version of TileShop reads format {ProjectFormat.Current} and older.");
        else
            return true;

        return false;
    }

    private bool TryDeserializeResource(XDocument doc, string xmlFileName, [MaybeNullWhen(false)] out ResourceModel model)
    {
        var resourceName = Path.GetFileNameWithoutExtension(xmlFileName);
        model = default;

        try
        {
            switch (doc.Root.Name.LocalName)
            {
                case "datafile":
                    TryDeserializeDataFile(doc.Root, resourceName, out var dataFileModel);
                    model = dataFileModel;
                    return true;
                case "palette":
                    if (!TryDeserializePalette(doc.Root, xmlFileName, resourceName, out var paletteModel))
                        return false;
                    model = paletteModel;
                    return true;
                case "arranger":
                    TryDeserializeScatteredArranger(doc.Root, resourceName, out var arrangerModel);
                    model = arrangerModel;
                    return true;
            }
        }
        catch (Exception ex)
        {
            _errors.Add($"An exception occurred while reading '{xmlFileName}': {ex.Message}");
            return false;
        }

        return false;
    }

    private string LocateParentPathKey(string fullFileName)
    {
        var path = Directory.GetParent(fullFileName);
        var relativePath = Path.GetRelativePath(_baseDirectory, path.FullName);
        return string.Join('/', relativePath.Split('\\'));
    }

    private static ImageProjectModel DeserializeProject(XElement element, string resourceName) =>
        new()
        {
            Name = resourceName,
            Root = element.Attribute("root")?.Value ?? string.Empty
        };

    private bool TryDeserializeDataFile(XElement element, string resourceName, out DataFileModel dataFileModel)
    {
        var model = new DataFileModel
        {
            Name = resourceName,
            Location = Path.Combine(_baseDirectory, element.Attribute("location").Value)
        };

        dataFileModel = model;
        return true;
    }

    private bool TryDeserializePalette(XElement element, string xmlFileName, string resourceName, out PaletteModel paletteModel)
    {
        var model = new PaletteModel()
        {
            Name = resourceName,
        };
        model.DataFileKey = element.Attribute("datafile").Value;
        model.ColorModel = Palette.StringToColorModel(element.Attribute("color").Value);
        model.ZeroIndexTransparent = bool.Parse(element.Attribute("zeroindextransparent").Value);
        paletteModel = model;

        var colorIndex = 0;
        foreach (var item in element.Elements())
        {
            if (item.Name.LocalName == "filesource")
            {
                var source = new FileColorSourceModel();
                var fileOffset = long.Parse(item.Attribute("fileoffset").Value, System.Globalization.NumberStyles.HexNumber);
                source.FileAddress = fileOffset;

                source.Entries = int.Parse(item.Attribute("entries").Value);
                colorIndex += source.Entries;

                if (item.Attribute("endian") is not null)
                {
                    if (item.Attribute("endian").Value == "big")
                        source.Endian = Endian.Big;
                    else if (item.Attribute("endian").Value == "little")
                        source.Endian = Endian.Little;
                    else
                        _errors.Add($"'endian' has unknown value '{item.Attribute("endian").Value}'");
                }

                model.ColorSources.Add(source);
            }
            else if (item.Name.LocalName == "nativecolor")
            {
                var value = item.Attribute("value").Value;
                if (!ColorParser.TryParse(value, ColorModel.Rgba32, out var nativeColor))
                    return FailColor(value, ColorModel.Rgba32);

                model.ColorSources.Add(new ProjectNativeColorSourceModel((ColorRgba32)nativeColor));
                colorIndex++;
            }
            else if (item.Name.LocalName == "foreigncolor")
            {
                var value = item.Attribute("value").Value;
                if (!ColorParser.TryParse(value, model.ColorModel, out var foreignColor))
                    return FailColor(value, model.ColorModel);

                model.ColorSources.Add(new ProjectForeignColorSourceModel(foreignColor));
                colorIndex++;
            }
            else if (item.Name.LocalName == "scatteredcolor")
            {
                var entries = int.Parse(item.Attribute("entries").Value);
                var runs = item.Elements().Select(ReadField).ToList();

                try
                {
                    Palette.ValidateScatteredFields(ScatteredColorSource.ExpandRun(entries, runs), model.ColorModel, _colorFactory);
                }
                catch (ColorFieldException ex)
                {
                    _errors.Add($"'{xmlFileName}' entry {colorIndex + (ex.Entry ?? 0)} field {ex.Field}: {ex.Reason}");
                    return false;
                }

                model.ColorSources.Add(new ScatteredColorSourceModel(entries, runs));
                colorIndex += entries;
            }
        }

        if (colorIndex > Palette.MaxEntries)
        {
            _errors.Add($"'{xmlFileName}' has {colorIndex} colors, more than the {Palette.MaxEntries} a palette can hold");
            return false;
        }

        return true;

        static ColorFieldRun ReadField(XElement field)
        {
            var offset = long.Parse(field.Attribute("fileoffset").Value, System.Globalization.NumberStyles.HexNumber);
            var lowBit = int.Parse(field.Attribute("lowbit")?.Value ?? "0");
            var bits = int.Parse(field.Attribute("bits").Value);
            var shift = int.Parse(field.Attribute("shift")?.Value ?? "0");
            var stride = int.Parse(field.Attribute("stride")?.Value ?? "8");
            return new ColorFieldRun(new ColorField(offset, lowBit, bits, shift), stride);
        }

        bool FailColor(string value, ColorModel colorModel)
        {
            _errors.Add($"'{xmlFileName}' entry {colorIndex}: '{value}' is not a valid {colorModel} color");
            return false;
        }
    }

    private bool TryDeserializeScatteredArranger(XElement element, string resourceName, out ScatteredArrangerModel arrangerModel)
    {
        var elementsx = int.Parse(element.Attribute("elementsx").Value); // Width of arranger in elements
        var elementsy = int.Parse(element.Attribute("elementsy").Value); // Height of arranger in elements

        var model = new ScatteredArrangerModel()
        {
            Name = resourceName,
            ElementGrid = new ArrangerElementModel[elementsx, elementsy],
            ArrangerElementSize = new Size(elementsx, elementsy),
        };

        var width = int.Parse(element.Attribute("width").Value); // Width of element in pixels
        var height = int.Parse(element.Attribute("height").Value); // Height of element in pixels
        var defaultCodecName = element.Attribute("defaultcodec").Value;
        var defaultDataFileKey = element.Attribute("defaultdatafile").Value;
        var defaultPaletteKey = element.Attribute("defaultpalette")?.Value ?? _globalDefaultPalette.Name;
        var layoutName = element.Attribute("layout").Value;
        var colorType = element.Attribute("color")?.Value ?? "indexed";
        var elementList = element.Descendants("element");

        if (layoutName == "tiled")
            model.Layout = ElementLayout.Tiled;
        else if (layoutName == "single")
            model.Layout = ElementLayout.Single;
        else
            throw new XmlException($"Unsupported arranger layout type ('{layoutName}') for arranger '{model.Name}'");

        if (colorType == "indexed")
            model.ColorType = PixelColorType.Indexed;
        else if (colorType == "direct")
            model.ColorType = PixelColorType.Direct;
        else
            throw new XmlException($"Unsupported pixel color type ('{colorType}') for arranger '{model.Name}'");

        model.ElementPixelSize = new Size(width, height);

        var xmlElements = elementList.Select(e => new
        {
            fileoffset = long.Parse(e.Attribute("fileoffset").Value, System.Globalization.NumberStyles.HexNumber),
            bitoffset = e.Attribute("bitoffset"),
            posx = int.Parse(e.Attribute("posx").Value),
            posy = int.Parse(e.Attribute("posy").Value),
            format = e.Attribute("codec"),
            palette = e.Attribute("palette"),
            datafile = e.Attribute("datafile"),
            mirror = e.Attribute("mirror"),
            rotation = e.Attribute("rotation")
        });

        foreach (var xmlElement in xmlElements)
        {
            var el = new ArrangerElementModel()
            {
                DataFileKey = xmlElement.datafile?.Value ?? defaultDataFileKey,
                PaletteKey = model.ColorType == PixelColorType.Direct ? null : xmlElement.palette?.Value ?? defaultPaletteKey,
                CodecName = xmlElement.format?.Value ?? defaultCodecName,
                PositionX = xmlElement.posx,
                PositionY = xmlElement.posy,
            };

            if (xmlElement.bitoffset is not null)
                el.FileAddress = new BitAddress(xmlElement.fileoffset, int.Parse(xmlElement.bitoffset.Value));
            else
                el.FileAddress = new BitAddress(xmlElement.fileoffset, 0);

            el.Mirror = xmlElement.mirror?.Value switch
            {
                "none" => MirrorOperation.None,
                "horizontal" => MirrorOperation.Horizontal,
                "vertical" => MirrorOperation.Vertical,
                "both" => MirrorOperation.Both,
                _ => MirrorOperation.None
            };

            el.Rotation = xmlElement.rotation?.Value switch
            {
                "none" => RotationOperation.None,
                "left" => RotationOperation.Left,
                "right" => RotationOperation.Right,
                "turn" => RotationOperation.Turn,
                _ => RotationOperation.None
            };

            model.ElementGrid[xmlElement.posx, xmlElement.posy] = el;
        }

        arrangerModel = model;
        return true;
    }
}
