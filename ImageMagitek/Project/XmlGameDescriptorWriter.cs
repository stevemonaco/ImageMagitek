using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ImageMagitek.Project.SerializationModels;
using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public class XmlGameDescriptorWriter : IGameDescriptorWriter
    {
        public string DescriptorVersion => "0.1";

        public bool WriteProject(PathTree<ProjectResourceBase> tree, string fileName)
        {
            if (tree is null)
                throw new ArgumentNullException($"{nameof(WriteProject)} property '{nameof(tree)}' was null");

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException($"{nameof(WriteProject)} property '{nameof(fileName)}' was null or empty");

            var xmlRoot = new XElement("gdf");
            xmlRoot.Add(new XAttribute("version", DescriptorVersion));
            var projectRoot = new XElement("project");
            xmlRoot.Add(projectRoot);

            var modelTree = BuildModelTree(tree);

            foreach(var node in modelTree.EnumerateDepthFirst())
            {
                XElement element;
 
                switch(node.Value)
                {
                    case ResourceFolderModel folderModel:
                        element = Serialize(folderModel);
                        break;
                    case DataFileModel dataFileModel:
                        element = Serialize(dataFileModel);
                        break;
                    case PaletteModel paletteModel:
                        element = Serialize(paletteModel);
                        break;
                    case ScatteredArrangerModel arrangerModel:
                        element = Serialize(arrangerModel);
                        break;
                    default:
                        throw new InvalidOperationException($"{nameof(WriteProject)}: unexpected node of type '{node.Value.GetType().ToString()}'");
                }

                AddResourceToXmlTree(projectRoot, element, node.Paths.ToArray());
            }

            xmlRoot.Save(fileName);

            return true;
        }

        private PathTree<ProjectNodeModel> BuildModelTree(PathTree<ProjectResourceBase> tree)
        {
            var resourceResolver = new Dictionary<ProjectResourceBase, string>();
            foreach (var node in tree.EnumerateDepthFirst())
                resourceResolver.Add(node.Value, node.PathKey);

            var modelTree = new PathTree<ProjectNodeModel>();

            foreach (var node in tree.EnumerateDepthFirst().Where(x => x.Value.ShouldBeSerialized && x.Value is ResourceFolder))
            {
                var folderModel = ResourceFolderModel.FromResourceFolder(node.Value as ResourceFolder);
                modelTree.Add(node.PathKey, folderModel);
            }

            foreach (var node in tree.EnumerateDepthFirst().Where(x => x.Value.ShouldBeSerialized))
            {
                switch (node.Value)
                {
                    case ResourceFolder folder:
                        break;
                    case DataFile dataFile:
                        var dataFileModel = DataFileModel.FromDataFile(dataFile);
                        modelTree.Add(node.PathKey, dataFileModel);
                        break;
                    case Palette palette:
                        var paletteModel = PaletteModel.FromPalette(palette);
                        paletteModel.DataFileKey = resourceResolver[palette.DataFile];
                        modelTree.Add(node.PathKey, paletteModel);
                        break;
                    case ScatteredArranger arranger:
                        var arrangerModel = ScatteredArrangerModel.FromScatteredArranger(arranger);
                        for (int x = 0; x < arrangerModel.ArrangerElementSize.Width; x++)
                        {
                            for (int y = 0; y < arrangerModel.ArrangerElementSize.Height; y++)
                            {
                                var element = arranger.ElementGrid[x, y];
                                var elementModel = arrangerModel.ElementGrid[x, y];
                                elementModel.DataFileKey = resourceResolver[element.DataFile];
                                elementModel.PaletteKey = resourceResolver[element.Palette];
                            }
                        }
                        modelTree.Add(node.PathKey, arrangerModel);
                        break;
                }
            }

            return modelTree;
        }

        private void AddResourceToXmlTree(XElement projectNode, XElement resourceNode, string[] resourcePaths)
        {
            var nodeVisitor = projectNode;

            foreach(var path in resourcePaths.Take(resourcePaths.Length - 1))
            {
                nodeVisitor = nodeVisitor.Elements().Where(x => (string) x.Attribute("name") == path).FirstOrDefault();
                if (nodeVisitor is null)
                    throw new KeyNotFoundException($"{nameof(AddResourceToXmlTree)}: node with path '{path}' not found");
            }

            nodeVisitor.Add(resourceNode);
        }

        private XElement Serialize(ResourceFolderModel folderModel)
        {
            return new XElement("folder", new XAttribute("name", folderModel.Name));
        }

        private XElement Serialize(DataFileModel dataFileModel)
        {
            var element = new XElement("datafile");
            element.Add(new XAttribute("name", dataFileModel.Name));
            element.Add(new XAttribute("location", dataFileModel.Location));
            return element;
        }

        private XElement Serialize(PaletteModel paletteModel)
        {
            var element = new XElement("palette");
            element.Add(new XAttribute("name", paletteModel.Name));
            element.Add(new XAttribute("fileoffset", $"{paletteModel.FileAddress.FileOffset:X}"));
            element.Add(new XAttribute("datafile", paletteModel.DataFileKey));
            element.Add(new XAttribute("format", paletteModel.ColorModel.ToString()));
            element.Add(new XAttribute("entries", paletteModel.Entries));
            element.Add(new XAttribute("zeroindextransparent", paletteModel.ZeroIndexTransparent));

            return element;
        }

        private XElement Serialize(ScatteredArrangerModel arrangerModel)
        {
            var defaultFormat = arrangerModel.FindMostFrequentPropertyValue("CodecName");
            var defaultPalette = arrangerModel.FindMostFrequentPropertyValue("PaletteKey");
            var defaultFile = arrangerModel.FindMostFrequentPropertyValue("DataFileKey");

            var arrangerNode = new XElement("arranger");
            arrangerNode.Add(new XAttribute("name", arrangerModel.Name));
            arrangerNode.Add(new XAttribute("elementsx", arrangerModel.ArrangerElementSize.Width));
            arrangerNode.Add(new XAttribute("elementsy", arrangerModel.ArrangerElementSize.Height));
            arrangerNode.Add(new XAttribute("width", arrangerModel.ElementPixelSize.Width));
            arrangerNode.Add(new XAttribute("height", arrangerModel.ElementPixelSize.Height));

            if(arrangerModel.Layout == ArrangerLayout.TiledArranger)
                arrangerNode.Add(new XAttribute("layout", "tiled"));
            else if (arrangerModel.Layout == ArrangerLayout.LinearArranger)
                arrangerNode.Add(new XAttribute("layout", "linear"));

            arrangerNode.Add(new XAttribute("defaultformat", defaultFormat));
            arrangerNode.Add(new XAttribute("defaultdatafile", defaultFile));
            arrangerNode.Add(new XAttribute("defaultpalette", defaultPalette));

            for(int y = 0; y < arrangerModel.ArrangerElementSize.Height; y++)
            {
                for(int x = 0; x < arrangerModel.ArrangerElementSize.Width; x++)
                {
                    var el = arrangerModel.ElementGrid[x, y];

                    var elNode = new XElement("element");
                    elNode.Add(new XAttribute("fileoffset", $"{el.FileAddress.FileOffset:X}"));
                    elNode.Add(new XAttribute("posx", el.PositionX));
                    elNode.Add(new XAttribute("posy", el.PositionY));

                    if(el.CodecName != defaultFormat)
                    elNode.Add(new XAttribute("format", el.CodecName));

                    if(el.DataFileKey != defaultFile)
                        elNode.Add(new XAttribute("datafile", el.DataFileKey));

                    if(el.PaletteKey != defaultPalette)
                        elNode.Add(new XAttribute("palette", el.PaletteKey));

                    arrangerNode.Add(elNode);
                }
            }

            return arrangerNode;
        }


        /*
        public static void SerializeProject(Dictionary<string, ProjectResourceBase> projectTree, Stream stream)
        {
            if (projectTree is null)
                throw new ArgumentNullException($"SerializeProject was called with a null {nameof(projectTree)}");
            if (stream is null)
                throw new ArgumentNullException("SerializeProject was called with a null stream");
            if (!stream.CanWrite)
                throw new ArgumentException("SerializeProject was called with a stream without write access");

            var xmlRoot = new XElement("gdf");
            var projectRoot = new XElement("project");
            var settingsRoot = new XElement("settings");

            xmlRoot.Add(settingsRoot);
            xmlRoot.Add(projectRoot);

            var orderedNodes = projectTree.Values.OrderBy(x => x, new ProjectResourceBaseComparer()).Where(x => x.ShouldBeSerialized);
            orderedNodes.ForEach(x => projectRoot.Add(x.Serialize()));

            xmlRoot.Save(stream);
        } */
    }
}
