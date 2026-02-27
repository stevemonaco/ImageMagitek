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
    public string Version => "0.9";

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

        if (!TryDeserializeXmlFile(projectFileName, _resourceSchema, out var rootModel))
        {
            return new MagitekResults<ProjectTree>.Failed(_errors);
        }

        if (rootModel is not ImageProjectModel projectModel)
        {
            _errors.Add($"'{projectFileName}' was expected to be a project file");
            return new MagitekResults<ProjectTree>.Failed(_errors);
        }

        var builder = new ProjectTreeBuilder(_codecFactory, _colorFactory, _globalDefaultPalette, _globalResources);

        _baseDirectory = Path.GetDirectoryName(projectFileName);
        if (!string.IsNullOrWhiteSpace(projectModel.Root))
        {
            if (Path.IsPathFullyQualified(projectModel.Root))
                _baseDirectory = projectModel.Root;
            else
                _baseDirectory = Path.Combine(_baseDirectory, projectModel.Root);
        }

        builder.AddProject(projectModel, _baseDirectory, projectFileName);

        // Fail if an incomplete transaction journal exists — recovery must be performed first
        var journalPath = Path.Combine(_baseDirectory, "_transaction.json");
        if (File.Exists(journalPath))
        {
            _errors.Add($"Project directory contains an incomplete transaction journal '{journalPath}'. Recovery must be performed before the project can be loaded.");
            return new MagitekResults<ProjectTree>.Failed(_errors);
        }

        // Add directories
        var directoryNames = Directory.GetDirectories(_baseDirectory, "*", SearchOption.AllDirectories);
        foreach (var directoryName in directoryNames)
        {
            var folderModel = new ResourceFolderModel()
            {
                Name = Path.GetFileName(directoryName)
            };

            var parentDirectory = Directory.GetParent(directoryName).FullName;
            var relativePath = Path.GetRelativePath(_baseDirectory, parentDirectory);
            var parentKey = relativePath == "." ? "" : relativePath;
            builder.AddFolder(folderModel, parentKey, Path.GetFullPath(directoryName));
        }

        // Add resources
        string fullProjectFileName = new FileInfo(projectFileName).FullName;
        var resourceFileNames = Directory.GetFiles(_baseDirectory, "*.xml", SearchOption.AllDirectories)
            .Except(new[] { fullProjectFileName })
            .ToList();

        List<(ResourceModel model, string pathKey, string fileLocation)> resourceModels = new();

        foreach (var resourceFileName in resourceFileNames)
        {
            if (!TryDeserializeXmlFile(resourceFileName, _resourceSchema, out var resourceModel))
            {
                return new MagitekResults<ProjectTree>.Failed(_errors);
            }

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
                _errors.Add($"{result.AsError.Reason}");
            }
        }

        if (_errors.Count > 0)
            return new MagitekResults<ProjectTree>.Failed(_errors);

        return new MagitekResults<ProjectTree>.Success(builder.Tree);
    }

    private bool TryDeserializeXmlFile(string xmlFileName, XmlSchemaSet schema, [MaybeNullWhen(false)] out ResourceModel model)
    {
        if (!File.Exists(xmlFileName))
        {
            _errors.Add($"File '{xmlFileName}' does not exist");
            model = default;
            return false;
        }

        try
        {
            var xml = File.ReadAllText(xmlFileName);
            var doc = XDocument.Parse(xml, LoadOptions.SetLineInfo);

            if (schema is not null)
            {
                doc.Validate(schema, (o, e) =>
                {
                    var line = o as IXmlLineInfo;
                    _errors.Add($"'{xmlFileName}' line {line?.LineNumber.ToString() ?? "Unknown"}: {e.Message}");
                });

                if (_errors.Any())
                {
                    model = default;
                    return false;
                }
            }

            var resourceName = Path.GetFileNameWithoutExtension(xmlFileName);
            var rootElementName = doc.Root.Name.LocalName;

            if (rootElementName == "project")
            {
                TryDeserializeProject(doc.Root, resourceName, out var projectModel);
                model = projectModel;
            }
            else if (rootElementName == "datafile")
            {
                TryDeserializeDataFile(doc.Root, resourceName, out var dataFileModel);
                model = dataFileModel;
            }
            else if (rootElementName == "palette")
            {
                TryDeserializePalette(doc.Root, resourceName, out var paletteModel);
                model = paletteModel;
            }
            else if (rootElementName == "arranger")
            {
                TryDeserializeScatteredArranger(doc.Root, resourceName, out var arrangerModel);
                model = arrangerModel;
            }
            else
            {
                _errors.Add($"{xmlFileName} has invalid root element '{rootElementName}'");
                model = null;
                return false;
            }

            return true;
        }
        catch (XmlSchemaValidationException vex)
        {
            _errors.Add($"Validation error on line {vex.LineNumber}: '{vex.Message}'");
        }
        catch (Exception ex)
        {
            _errors.Add($"An exception occurred while reading '{xmlFileName}': {ex.Message}");
        }

        model = default;
        return false;
    }

    private string LocateResourceOnDisk(string location)
    {
        return Path.Join(_baseDirectory, location);
    }

    private string LocateParentPathKey(string fullFileName)
    {
        var path = Directory.GetParent(fullFileName);
        var relativePath = Path.GetRelativePath(_baseDirectory, path.FullName);
        return string.Join('/', relativePath.Split('\\'));
    }

    private string LocatePathKey(string fullFileName)
    {
        var relativePath = Path.GetRelativePath(_baseDirectory, fullFileName);
        return string.Join('/', relativePath.Split('\\').Skip(1));
    }

    private static bool TryDeserializeProject(XElement element, string resourceName, out ImageProjectModel projectModel)
    {
        var model = new ImageProjectModel
        {
            Name = resourceName,
            Version = decimal.Parse(element.Attribute("version").Value),
            Root = element.Attribute("root")?.Value ?? string.Empty
        };

        projectModel = model;
        return true;
    }

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

    private bool TryDeserializePalette(XElement element, string resourceName, out PaletteModel paletteModel)
    {
        var model = new PaletteModel()
        {
            Name = resourceName,
        };
        model.DataFileKey = element.Attribute("datafile").Value;
        model.ColorModel = Palette.StringToColorModel(element.Attribute("color").Value);
        model.ZeroIndexTransparent = bool.Parse(element.Attribute("zeroindextransparent").Value);

        foreach (var item in element.Elements())
        {
            if (item.Name.LocalName == "filesource")
            {
                var source = new FileColorSourceModel();
                var fileOffset = long.Parse(item.Attribute("fileoffset").Value, System.Globalization.NumberStyles.HexNumber);
                if (item.Attribute("bitoffset") is null)
                    source.FileAddress = new BitAddress(fileOffset, 0);
                else
                    source.FileAddress = new BitAddress(fileOffset, int.Parse(element.Attribute("bitoffset").Value));

                source.Entries = int.Parse(item.Attribute("entries").Value);

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
                if (ColorParser.TryParse(item.Attribute("value").Value, ColorModel.Rgba32, out var nativeColor))
                {
                    model.ColorSources.Add(new ProjectNativeColorSourceModel((ColorRgba32)nativeColor));
                }
            }
            else if (item.Name.LocalName == "foreigncolor")
            {
                if (ColorParser.TryParse(item.Attribute("value").Value, model.ColorModel, out var foreignColor))
                {
                    model.ColorSources.Add(new ProjectForeignColorSourceModel(foreignColor));
                }
            }
            else if (item.Name.LocalName == "scatteredcolor")
            { }
            else if (item.Name.LocalName == "import")
            { }
            else if (item.Name.LocalName == "export")
            { }
        }

        paletteModel = model;
        return true;
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
                PaletteKey = xmlElement.palette?.Value ?? defaultPaletteKey,
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
