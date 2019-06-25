using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Project.Models;
using System.Drawing;

namespace ImageMagitek.Project
{
    public class XmlGameDescriptorDeserializer : IGameDescriptorDeserializer
    {
        public IDictionary<string, ProjectResourceBase> DeserializeProject(string fileName, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException($"{nameof(DeserializeProject)} cannot have a null or empty value for '{nameof(fileName)}'");
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException($"{nameof(DeserializeProject)} cannot have a null or empty value for '{nameof(baseDirectory)}'");

            var stream = File.OpenRead(fileName);

            XElement doc = XElement.Load(stream);
            XElement projectNode = doc.Element("project");

            Directory.SetCurrentDirectory(baseDirectory);
            var resourceTree = new Dictionary<string, ProjectResourceBase>();

            foreach (var resource in DeserializeChildren(projectNode))
                resourceTree.Add(resource.Name, resource);

            return resourceTree;
        }

        private IEnumerable<ProjectResourceBase> DeserializeChildren(XElement element)
        {
            foreach (XElement node in element.Elements())
            {
                if (node.Name == "folder")
                {
                    var folder = DeserializeResourceFolder(node).ToResourceFolder();

                    foreach (var child in DeserializeChildren(node))
                    {
                        child.Parent = folder;
                        folder.ChildResources.Add(child.Name, child);
                    }
                    yield return folder;
                }
                else if (node.Name == "datafile")
                {
                    yield return DeserializeDataFile(node).ToDataFile();
                }
                else if (node.Name == "palette")
                {
                    var pal = DeserializePalette(node).ToPalette();
                    pal.LazyLoadPalette(pal.DataFileKey, pal.FileAddress, pal.ColorModel, pal.ZeroIndexTransparent, pal.Entries);
                    yield return pal;
                }
                else if (node.Name == "arranger")
                {
                    var arr = DeserializeScatteredArranger(node).ToScatteredArranger();
                    arr.Rename(node.Attribute("name").Value);
                    yield return arr;
                }
            }
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
            var defaultPalette = element.Attribute("defaultpalette").Value;
            var layoutName = element.Attribute("layout").Value;
            var elementList = element.Descendants("element");

            if (layoutName == "tiled")
                model.Layout = ArrangerLayout.TiledArranger;
            else if (layoutName == "linear")
                model.Layout = ArrangerLayout.LinearArranger;
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
                el.FormatName = xmlElement.format?.Value ?? defaultFormat;
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
