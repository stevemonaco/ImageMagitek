using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using ImageMagitek.ExtensionMethods;
using System.Drawing;
using Monaco.PathTree;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using System.Xml.Schema;
using System.Collections.Generic;

namespace ImageMagitek.Project.Serialization
{
    public class XmlGameDescriptorReader : IGameDescriptorReader
    {
        public string DescriptorVersion => "0.8";

        private readonly XmlSchemaSet _schemaSet;
        private readonly ICodecFactory _codecFactory;
        private readonly IColorFactory _colorFactory;
        private readonly List<IProjectResource> _globalResources;
        private readonly Palette _globalDefaultPalette;
        private string _baseDirectory;

        public XmlGameDescriptorReader(ICodecFactory codecFactory, IColorFactory colorFactory) : 
            this(new XmlSchemaSet(), codecFactory, colorFactory, Enumerable.Empty<IProjectResource>())
        {
        }

        public XmlGameDescriptorReader(XmlSchemaSet schemaSet, ICodecFactory codecFactory, IColorFactory colorFactory) : 
            this(schemaSet, codecFactory, colorFactory, Enumerable.Empty<IProjectResource>())
        {
        }

        public XmlGameDescriptorReader(XmlSchemaSet schemaSet, ICodecFactory codecFactory, 
            IColorFactory colorFactory, IEnumerable<IProjectResource> globalResources)
        {
            _schemaSet = schemaSet;
            _codecFactory = codecFactory;
            _colorFactory = colorFactory;
            _globalResources = globalResources.ToList();
            _globalDefaultPalette = _globalResources.OfType<Palette>().First();
        }
        
        public MagitekResults<ProjectTree> ReadProject(string projectFileName)
        {
            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(ReadProject)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            using var stream = File.OpenRead(projectFileName);
            _baseDirectory = Path.GetDirectoryName(stream.Name);

            var doc = XDocument.Load(stream, LoadOptions.SetLineInfo);

            if (_schemaSet is object)
            {
                var validationErrors = new List<string>();

                doc.Validate(_schemaSet, (o, e) =>
                {
                    validationErrors.Add(e.Message);
                });

                if (validationErrors.Any())
                    return new MagitekResults<ProjectTree>.Failed(validationErrors);
            }

            XElement projectNode = doc.Element("gdf").Element("project");
            var projectErrors = new List<string>();

            var projectModel = DeserializeImageProject(projectNode);
            var builder = new ProjectTreeBuilder(_codecFactory, _colorFactory, _globalResources);
            builder.AddProject(projectModel);

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

            foreach (var node in projectNode.Descendants("palette"))
            {
                var paletteModel = DeserializePalette(node);
                builder.AddPalette(paletteModel, node.NodePath());
            }

            foreach (var node in projectNode.Descendants("arranger"))
            {
                var arrangerModel = DeserializeScatteredArranger(node);
                builder.AddScatteredArranger(arrangerModel, node.NodePath());
            }

            if (projectErrors.Count > 0)
                return new MagitekResults<ProjectTree>.Failed(projectErrors);

            var tree = builder.Tree;
            var projectTree = new ProjectTree(tree, projectFileName);

            return new MagitekResults<ProjectTree>.Success(projectTree);
        }

        private ImageProjectModel DeserializeImageProject(XElement element)
        {
            var model = new ImageProjectModel
            {
                Name = element.Attribute("name").Value,
                Root = element.Attribute("root")?.Value ?? ""
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

        private PaletteModel DeserializePalette(XElement element)
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

            return model;
        }

        private ResourceFolderModel DeserializeResourceFolder(XElement element)
        {
            var model = new ResourceFolderModel
            {
                Name = element.Attribute("name").Value
            };

            return model;
        }

        private ScatteredArrangerModel DeserializeScatteredArranger(XElement element)
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

            return model;
        }
    }
}
