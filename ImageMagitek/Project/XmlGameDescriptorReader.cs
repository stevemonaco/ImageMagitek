using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using ImageMagitek.Project.SerializationModels;
using ImageMagitek.ExtensionMethods;
using System.Drawing;
using Monaco.PathTree;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.Project
{
    public class XmlGameDescriptorReader : IGameDescriptorReader
    {
        public string DescriptorVersion => "0.1";
        private ICodecFactory _codecFactory;

        public XmlGameDescriptorReader(ICodecFactory CodecFactory)
        {
            _codecFactory = CodecFactory;
        }

        public IPathTree<IProjectResource> ReadProject(string fileName, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException($"{nameof(ReadProject)} cannot have a null or empty value for '{nameof(fileName)}'");
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException($"{nameof(ReadProject)} cannot have a null or empty value for '{nameof(baseDirectory)}'");

            var stream = File.OpenRead(fileName);

            XElement doc = XElement.Load(stream);
            XElement projectNode = doc.Element("project");

            Directory.SetCurrentDirectory(baseDirectory);
            var tree = new PathTree<IProjectResource>();

            foreach(var node in projectNode.Descendants("folder"))
            {
                var res = DeserializeResourceFolder(node).ToResourceFolder();
                var path = Path.Combine(node.NodePath(), node.Attribute("name").Value);
                tree.Add(path, res);
            }

            foreach (var node in projectNode.Descendants("datafile"))
            {
                var res = DeserializeDataFile(node).ToDataFile();
                var path = Path.Combine(node.NodePath(), node.Attribute("name").Value);
                tree.Add(path, res);
            }

            foreach (var node in projectNode.Descendants("palette"))
            {
                var model = DeserializePalette(node);
                var pal = model.ToPalette();
                tree.TryGetValue<DataFile>(model.DataFileKey, out var df);
                pal.DataFile = df;
                pal.LazyLoadPalette(pal.DataFile, pal.FileAddress, pal.ColorModel, pal.ZeroIndexTransparent, pal.Entries);
                var path = Path.Combine(node.NodePath(), node.Attribute("name").Value);
                tree.Add(path, pal);
            }

            foreach (var node in projectNode.Descendants("arranger"))
            {
                var modelArranger = DeserializeScatteredArranger(node);
                var arranger = modelArranger.ToScatteredArranger();

                for(int x = 0; x < arranger.ArrangerElementSize.Width; x++)
                {
                    for(int y = 0; y < arranger.ArrangerElementSize.Height; y++)
                    {
                        if (modelArranger.ElementGrid[x, y] is null)
                        {
                            var el = new ArrangerElement(x * arranger.ArrangerElementSize.Width, y * arranger.ArrangerElementSize.Height,
                                null, 0, new BlankIndexedCodec(), null);
                            arranger.SetElement(el, x, y);
                            continue;
                        }

                        tree.TryGetValue<DataFile>(modelArranger.ElementGrid[x, y].DataFileKey, out var df);

                        Palette pal = default;
                        if(!string.IsNullOrEmpty(modelArranger.ElementGrid[x, y].PaletteKey))
                            tree.TryGetValue<Palette>(modelArranger.ElementGrid[x, y].PaletteKey, out pal);

                        var element = arranger.GetElement(x, y);

                        var codec = _codecFactory.GetCodec(modelArranger.ElementGrid[x, y].CodecName, arranger.ElementPixelSize.Width, arranger.ElementPixelSize.Height);
                        element = element.WithTarget(df, element.FileAddress, codec, pal);
                        arranger.SetElement(element, x, y);
                    }
                }

                var path = Path.Combine(node.NodePath(), node.Attribute("name").Value);
                tree.Add(path, arranger);
            }

            return tree;
        }

        private DataFileModel DeserializeDataFile(XElement element)
        {
            var model = new DataFileModel();

            model.Name = element.Attribute("name").Value;
            model.Location = element.Attribute("location").Value;

            return model;
        }

        private PaletteModel DeserializePalette(XElement element)
        {
            var model = new PaletteModel();

            model.Name = element.Attribute("name").Value;
            var fileOffset = long.Parse(element.Attribute("fileoffset").Value, System.Globalization.NumberStyles.HexNumber);
            model.DataFileKey = element.Attribute("datafile").Value;
            model.Entries = int.Parse(element.Attribute("entries").Value);
            model.ColorModel = Palette.StringToColorModel(element.Attribute("format").Value);
            model.ZeroIndexTransparent = bool.Parse(element.Attribute("zeroindextransparent").Value);

            if (element.Attribute("bitoffset") is null)
                model.FileAddress = new FileBitAddress(fileOffset, 0);
            else
                model.FileAddress = new FileBitAddress(fileOffset, int.Parse(element.Attribute("bitoffset").Value));

            return model;
        }

        private ResourceFolderModel DeserializeResourceFolder(XElement element)
        {
            var model = new ResourceFolderModel();
            model.Name = element.Attribute("name").Value;
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
            var defaultFormat = element.Attribute("defaultformat").Value;
            var defaultDataFile = element.Attribute("defaultdatafile").Value;
            var defaultPalette = element.Attribute("defaultpalette")?.Value ?? "";
            var layoutName = element.Attribute("layout").Value;
            var elementList = element.Descendants("element");

            if (layoutName == "tiled")
                model.Layout = ArrangerLayout.Tiled;
            else if (layoutName == "linear")
                model.Layout = ArrangerLayout.Single;
            else
                throw new XmlException($"Unsupported arranger layout type ('{layoutName}') for arranger '{model.Name}'");

            model.ArrangerElementSize = new Size(elementsx, elementsy);
            model.ElementGrid = new ArrangerElementModel[elementsx, elementsy];
            model.ElementPixelSize = new Size(width, height);

            var xmlElements = elementList.Select(e => new
            {
                fileoffset = long.Parse(e.Attribute("fileoffset").Value, System.Globalization.NumberStyles.HexNumber),
                bitoffset = e.Attribute("bitoffset"),
                posx = int.Parse(e.Attribute("posx").Value),
                posy = int.Parse(e.Attribute("posy").Value),
                format = e.Attribute("format"),
                palette = e.Attribute("palette"),
                file = e.Attribute("file")
            });

            foreach (var xmlElement in xmlElements)
            {
                var el = new ArrangerElementModel();

                el.DataFileKey = xmlElement.file?.Value ?? defaultDataFile;
                el.PaletteKey = xmlElement.palette?.Value ?? defaultPalette;
                el.CodecName = xmlElement.format?.Value ?? defaultFormat;
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
