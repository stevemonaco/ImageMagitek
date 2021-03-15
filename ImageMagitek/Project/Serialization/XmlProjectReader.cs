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

namespace ImageMagitek.Project.Serialization
{
    public class XmlProjectReader : IProjectReader
    {
        public string Version => "0.9";

        private readonly XmlSchemaSet _resourceSchema;

        private readonly ICodecFactory _codecFactory;
        private readonly IColorFactory _colorFactory;
        private readonly List<IProjectResource> _globalResources;
        private readonly Palette _globalDefaultPalette;

        private List<string> Errors;
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

        private bool TryDeserializeXmlFile(string xmlFileName, XmlSchemaSet schema, out ResourceModel model)
        {
            if (!File.Exists(xmlFileName))
            {
                Errors.Add($"File '{xmlFileName}' does not exist");
                model = default;
                return false;
            }

            try
            {
                var xml = File.ReadAllText(xmlFileName);
                var doc = XDocument.Parse(xml, LoadOptions.SetLineInfo);

                if (schema is object)
                {
                    doc.Validate(schema, (o, e) =>
                    {
                        Errors.Add(e.Message);
                    });

                    if (Errors.Any())
                    {
                        model = default;
                        return false;
                    }
                }

                var rootName = doc.Root.Name.LocalName;

                if (rootName == "project")
                {
                    TryDeserializeProject(doc.Root, out var projectModel);
                    model = projectModel;
                }
                else if (rootName == "datafile")
                {
                    TryDeserializeDataFile(doc.Root, out var dataFileModel);
                    model = dataFileModel;
                }
                else if (rootName == "palette")
                {
                    TryDeserializePalette(doc.Root, out var paletteModel);
                    model = paletteModel;
                }
                else if (rootName == "arranger")
                {
                    TryDeserializeScatteredArranger(doc.Root, out var arrangerModel);
                    model = arrangerModel;
                }
                else
                {
                    Errors.Add($"{xmlFileName} has invalid root element '{rootName}'");
                    model = null;
                    return false;
                }    

                return true;
            }
            catch (XmlSchemaValidationException vex)
            {
                Errors.Add($"Validation error on line {vex.LineNumber}: '{vex.Message}'");
            }
            catch (Exception ex)
            {
                Errors.Add($"An exception occurred while reading '{xmlFileName}': {ex.Message}");
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

        public MagitekResults<ProjectTree> ReadProject(string projectFileName)
        {
            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(ReadProject)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            Errors = new();

            if (!TryDeserializeXmlFile(projectFileName, _resourceSchema, out var rootModel))
            {
                return new MagitekResults<ProjectTree>.Failed(Errors);
            }

            if (rootModel is not ImageProjectModel projectModel)
            {
                Errors.Add($"'{projectFileName}' was expected to be a project file");
                return new MagitekResults<ProjectTree>.Failed(Errors);
            }

            var builder = new ProjectTreeBuilder(_codecFactory, _colorFactory, _globalResources);
            _baseDirectory = Path.GetDirectoryName(projectFileName);
            if (!string.IsNullOrWhiteSpace(projectModel.Root))
            {
                if (Path.IsPathFullyQualified(projectModel.Root))
                    _baseDirectory = projectModel.Root;
                else
                    _baseDirectory = Path.Combine(_baseDirectory, projectModel.Root);
            }

            builder.AddProject(projectModel, _baseDirectory, projectFileName);

            // Add directories
            var directoryNames = Directory.GetDirectories(_baseDirectory, "*", SearchOption.AllDirectories);
            foreach (var directoryName in directoryNames)
            {
                var folderModel = new ResourceFolderModel();
                folderModel.Name = Path.GetFileName(directoryName);
                
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

            var resourceModels = new List<(ResourceModel model, string pathKey, string fileLocation)>();

            foreach (var resourceFileName in resourceFileNames)
            {
                if (!TryDeserializeXmlFile(resourceFileName, _resourceSchema, out var resourceModel))
                {
                    return new MagitekResults<ProjectTree>.Failed(Errors);
                }

                var pathKey = LocateParentPathKey(resourceFileName);
                if (string.IsNullOrWhiteSpace(pathKey) || pathKey == ".")
                    pathKey = string.Empty;
                resourceModels.Add((resourceModel, pathKey, resourceFileName));
            }

            foreach (var (model, pathKey, fileLocation) in resourceModels.Where(x => x.model is DataFileModel))
            {
                builder.AddDataFile(model as DataFileModel, pathKey, fileLocation);
            }

            foreach (var (model, pathKey, fileLocation) in resourceModels.Where(x => x.model is PaletteModel))
            {
                builder.AddPalette(model as PaletteModel, pathKey, fileLocation);
            }

            foreach (var (model, pathKey, fileLocation) in resourceModels.Where(x => x.model is ScatteredArrangerModel))
            {
                builder.AddScatteredArranger(model as ScatteredArrangerModel, pathKey, fileLocation);
            }

            if (Errors.Count > 0)
                return new MagitekResults<ProjectTree>.Failed(Errors);

            return new MagitekResults<ProjectTree>.Success(builder.Tree);
        }

        private static bool TryDeserializeProject(XElement element, out ImageProjectModel projectModel)
        {
            var model = new ImageProjectModel
            {
                Name = element.Attribute("name").Value,
                Version = decimal.Parse(element.Attribute("version").Value),
                Root = element.Attribute("root")?.Value ?? string.Empty
            };

            projectModel = model;
            return true;
        }

        private bool TryDeserializeDataFile(XElement element, out DataFileModel dataFileModel)
        {
            var model = new DataFileModel
            {
                Name = element.Attribute("name").Value,
                Location = Path.Combine(_baseDirectory, element.Attribute("location").Value)
            };

            dataFileModel = model;
            return true;
        }

        private bool TryDeserializePalette(XElement element, out PaletteModel paletteModel)
        {
            var model = new PaletteModel();

            model.Name = element.Attribute("name").Value;
            var fileOffset = long.Parse(element.Attribute("fileoffset").Value, System.Globalization.NumberStyles.HexNumber);
            model.DataFileKey = element.Attribute("datafile").Value;
            model.Entries = int.Parse(element.Attribute("entries").Value);
            model.ColorModel = Palette.StringToColorModel(element.Attribute("color").Value);
            model.ZeroIndexTransparent = bool.Parse(element.Attribute("zeroindextransparent").Value);

            if (element.Attribute("bitoffset") is null)
                model.FileAddress = new FileBitAddress(fileOffset, 0);
            else
                model.FileAddress = new FileBitAddress(fileOffset, int.Parse(element.Attribute("bitoffset").Value));

            paletteModel = model;
            return true;
        }

        private bool TryDeserializeResourceFolder(XElement element, out ResourceFolderModel folderModel)
        {
            var model = new ResourceFolderModel
            {
                Name = element.Attribute("name").Value
            };

            folderModel = model;
            return true;
        }

        private bool TryDeserializeScatteredArranger(XElement element, out ScatteredArrangerModel arrangerModel)
        {
            var model = new ScatteredArrangerModel();

            model.Name = element.Attribute("name").Value;
            var elementsx = int.Parse(element.Attribute("elementsx").Value); // Width of arranger in elements
            var elementsy = int.Parse(element.Attribute("elementsy").Value); // Height of arranger in elements
            var width = int.Parse(element.Attribute("width").Value); // Width of element in pixels
            var height = int.Parse(element.Attribute("height").Value); // Height of element in pixels
            var defaultCodecName = element.Attribute("defaultcodec").Value;
            var defaultDataFileKey = element.Attribute("defaultdatafile").Value;
            var defaultPaletteKey = element.Attribute("defaultpalette")?.Value ?? "";
            var layoutName = element.Attribute("layout").Value;
            var colorType = element.Attribute("color")?.Value ?? "indexed";
            var elementList = element.Descendants("element");

            if (layoutName == "tiled")
                model.Layout = ArrangerLayout.Tiled;
            else if (layoutName == "single")
                model.Layout = ArrangerLayout.Single;
            else
                throw new XmlException($"Unsupported arranger layout type ('{layoutName}') for arranger '{model.Name}'");

            if (colorType == "indexed")
                model.ColorType = PixelColorType.Indexed;
            else if (colorType == "direct")
                model.ColorType = PixelColorType.Direct;
            else
                throw new XmlException($"Unsupported pixel color type ('{colorType}') for arranger '{model.Name}'");

            model.ArrangerElementSize = new Size(elementsx, elementsy);
            model.ElementGrid = new ArrangerElementModel[elementsx, elementsy];
            model.ElementPixelSize = new Size(width, height);

            var xmlElements = elementList.Select(e => new
            {
                fileoffset = long.Parse(e.Attribute("fileoffset").Value, System.Globalization.NumberStyles.HexNumber),
                bitoffset = e.Attribute("bitoffset"),
                posx = int.Parse(e.Attribute("posx").Value),
                posy = int.Parse(e.Attribute("posy").Value),
                format = e.Attribute("codec"),
                palette = e.Attribute("palette"),
                datafile = e.Attribute("datafile")
            });

            foreach (var xmlElement in xmlElements)
            {
                var el = new ArrangerElementModel();

                el.DataFileKey = xmlElement.datafile?.Value ?? defaultDataFileKey;
                el.PaletteKey = xmlElement.palette?.Value ?? defaultPaletteKey;
                el.CodecName = xmlElement.format?.Value ?? defaultCodecName;
                el.PositionX = xmlElement.posx;
                el.PositionY = xmlElement.posy;

                if (xmlElement.bitoffset != null)
                    el.FileAddress = new FileBitAddress(xmlElement.fileoffset, int.Parse(xmlElement.bitoffset.Value));
                else
                    el.FileAddress = new FileBitAddress(xmlElement.fileoffset, 0);

                model.ElementGrid[xmlElement.posx, xmlElement.posy] = el;
            }

            arrangerModel = model;
            return true;
        }
    }
}
