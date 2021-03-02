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
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek.Project.Serialization
{
    public class XmlGameDescriptorMultiFileReader : IGameDescriptorReader
    {
        public string DescriptorVersion => "0.9";

        private readonly XmlSchemaSet _projectSchema;
        private readonly XmlSchemaSet _resourceSchema;

        private readonly ICodecFactory _codecFactory;
        private readonly IColorFactory _colorFactory;
        private readonly List<IProjectResource> _globalResources;
        private readonly Palette _globalDefaultPalette;

        private List<string> Errors;
        private string _baseDirectory;

        public XmlGameDescriptorMultiFileReader(XmlSchemaSet projectSchema, XmlSchemaSet resourceSchema, 
            ICodecFactory codecFactory, IColorFactory colorFactory, IEnumerable<IProjectResource> globalResources)
        {
            _projectSchema = projectSchema;
            _resourceSchema = resourceSchema;
            _codecFactory = codecFactory;
            _colorFactory = colorFactory;
            _globalResources = globalResources.ToList();
            _globalDefaultPalette = _globalResources.OfType<Palette>().First();
        }

        private bool TryOpenXmlFile(string xmlFileName, XmlSchemaSet schema, out XDocument document)
        {
            if (!File.Exists(xmlFileName))
            {
                Errors.Add($"File '{xmlFileName}' does not exist");
                document = default;
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
                        document = default;
                        return false;
                    }
                }

                document = doc;
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

            document = default;
            return false;
        }
        
        private string LocateResource(string location)
        {
            return Path.Join(_baseDirectory, location);
        }

        public MagitekResults<ProjectTree> ReadProject(string projectFileName)
        {
            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(ReadProject)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            Errors = new();
            _baseDirectory = Path.GetDirectoryName(Path.GetFullPath(projectFileName));

            if (!TryOpenXmlFile(projectFileName, _projectSchema, out var doc))
            {
                return new MagitekResults<ProjectTree>.Failed(Errors);
            }

            XElement projectNode = doc.Element("gdf").Element("project");

            var projectModel = DeserializeImageProject(projectNode);
            var builder = new ProjectTreeBuilder(_codecFactory, _colorFactory, _globalResources);
            builder.AddProject(projectModel);

            if (!string.IsNullOrWhiteSpace(projectModel.Root))
            {
                if (Path.IsPathFullyQualified(projectModel.Root))
                    _baseDirectory = projectModel.Root;
                else
                    Path.Combine(_baseDirectory, projectModel.Root);
            }

            foreach (var node in projectNode.Descendants("folder"))
            {
                var folderModel = DeserializeResourceFolder(node);
                builder.AddFolder(folderModel, node.NodePath());
            }

            foreach (var node in projectNode.Descendants("datafile"))
            {
                var dfModel = DeserializeDataFile(node);
                builder.AddDataFile(dfModel, node.NodePath());
            }

            foreach (var node in projectNode.Descendants("paletteref"))
            {
                var location = node.Attribute("location").Value;
                var fileName = LocateResource(location);

                if (TryDeserializePalette(fileName, out var paletteModel))
                {
                    builder.AddPalette(paletteModel, node.NodePath());
                }
            }

            foreach (var node in projectNode.Descendants("arrangerref"))
            {
                var location = node.Attribute("location").Value;
                var fileName = LocateResource(location);

                if (TryDeserializeScatteredArranger(fileName, out var arrangerModel))
                {
                    builder.AddScatteredArranger(arrangerModel, node.NodePath());
                }
            }

            if (Errors.Count > 0)
                return new MagitekResults<ProjectTree>.Failed(Errors);

            return new MagitekResults<ProjectTree>.Success(builder.Tree);
        }

        private static ImageProjectModel DeserializeImageProject(XElement element)
        {
            var model = new ImageProjectModel
            {
                Name = element.Attribute("name").Value,
                Root = element.Attribute("root")?.Value ?? null
            };

            return model;
        }

        private DataFileModel DeserializeDataFile(XElement element)
        {
            var model = new DataFileModel
            {
                Name = element.Attribute("name").Value,
                Location = Path.Combine(_baseDirectory, element.Attribute("location").Value)
            };

            return model;
        }

        private bool TryDeserializePalette(string fileName, out PaletteModel paletteModel)
        {
            if (!TryOpenXmlFile(fileName, _resourceSchema, out var doc))
            {
                paletteModel = default;
                return false;
            }

            if (doc.Root is XElement element && element.Name.LocalName.Equals("palette"))
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
            else
            {
                Errors.Add($"File '{fileName}' has an unexpected root element of '{doc.Root}'");
                paletteModel = default;
                return false;
            }
        }

        private ResourceFolderModel DeserializeResourceFolder(XElement element)
        {
            var model = new ResourceFolderModel
            {
                Name = element.Attribute("name").Value
            };

            return model;
        }

        private bool TryDeserializeScatteredArranger(string fileName, out ScatteredArrangerModel arrangerModel)
        {
            if (!TryOpenXmlFile(fileName, _resourceSchema, out var doc))
            {
                arrangerModel = default;
                return false;
            }

            if (doc.Root is XElement element && element.Name.LocalName.Equals("arranger"))
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
                    el.PositionX = xmlElement.posx * width;
                    el.PositionY = xmlElement.posy * height;

                    if (xmlElement.bitoffset != null)
                        el.FileAddress = new FileBitAddress(xmlElement.fileoffset, int.Parse(xmlElement.bitoffset.Value));
                    else
                        el.FileAddress = new FileBitAddress(xmlElement.fileoffset, 0);

                    model.ElementGrid[xmlElement.posx, xmlElement.posy] = el;
                }

                arrangerModel = model;
                return true;
            }
            else
            {
                arrangerModel = default;
                Errors.Add($"File '{fileName}' has an unexpected root element of '{doc.Root}'");
                return false;
            }
        }
    }
}
