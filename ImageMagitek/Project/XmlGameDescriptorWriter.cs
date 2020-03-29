using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using ImageMagitek.Project.SerializationModels;
using ImageMagitek.Colors;
using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public class XmlGameDescriptorWriter : IGameDescriptorWriter
    {
        public string DescriptorVersion => "0.8";

        public bool WriteProject(IPathTree<IProjectResource> tree, string fileName)
        {
            if (tree is null)
                throw new ArgumentNullException($"{nameof(WriteProject)} property '{nameof(tree)}' was null");

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException($"{nameof(WriteProject)} property '{nameof(fileName)}' was null or empty");

            var xmlRoot = new XElement("gdf");
            xmlRoot.Add(new XAttribute("version", DescriptorVersion));

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
                    case ImageProjectModel projectModel:
                        element = Serialize(projectModel);
                        break;
                    default:
                        throw new InvalidOperationException($"{nameof(WriteProject)}: unexpected node of type '{node.Value.GetType().ToString()}'");
                }

                AddResourceToXmlTree(xmlRoot, element, node.Paths.ToArray());
            }

            var xws = new XmlWriterSettings();
            xws.Indent = true;
            xws.IndentChars = "\t";

            using var fs = new FileStream(fileName, FileMode.Create);
            using var xw = XmlWriter.Create(fs, xws);
            xmlRoot.Save(xw);

            return true;
        }

        private IPathTree<ProjectNodeModel> BuildModelTree(IPathTree<IProjectResource> tree)
        {
            var resourceResolver = new Dictionary<IProjectResource, string>();
            foreach (var node in tree.EnumerateDepthFirst())
                resourceResolver.Add(node.Value, node.PathKey);

            var projectModel = ImageProjectModel.FromImageProject(tree.Root.Value as ImageProject);
            IPathTree<ProjectNodeModel> modelTree = new PathTree<ProjectNodeModel>(projectModel.Name, projectModel);

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
                                var element = arranger.GetElement(x, y);
                                var elementModel = arrangerModel.ElementGrid[x, y];

                                if (element.DataFile is object)
                                    elementModel.DataFileKey = resourceResolver[element.DataFile];
                                else
                                    continue;

                                if (element.Palette is object)
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

        private XElement Serialize(ImageProjectModel projectModel)
        {
            var element = new XElement("project");
            element.Add(new XAttribute("name", projectModel.Name));

            if (!string.IsNullOrEmpty(projectModel.Root))
                element.Add(new XAttribute("root", projectModel.Root));

            return element;
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
            element.Add(new XAttribute("color", paletteModel.ColorModel.ToString()));
            element.Add(new XAttribute("entries", paletteModel.Entries));
            element.Add(new XAttribute("zeroindextransparent", paletteModel.ZeroIndexTransparent));

            return element;
        }

        private XElement Serialize(ScatteredArrangerModel arrangerModel)
        {
            var defaultCodec = arrangerModel.FindMostFrequentPropertyValue("CodecName");
            var defaultFile = arrangerModel.FindMostFrequentPropertyValue("DataFileKey") ?? "";

            var arrangerNode = new XElement("arranger");
            arrangerNode.Add(new XAttribute("name", arrangerModel.Name));
            arrangerNode.Add(new XAttribute("elementsx", arrangerModel.ArrangerElementSize.Width));
            arrangerNode.Add(new XAttribute("elementsy", arrangerModel.ArrangerElementSize.Height));
            arrangerNode.Add(new XAttribute("width", arrangerModel.ElementPixelSize.Width));
            arrangerNode.Add(new XAttribute("height", arrangerModel.ElementPixelSize.Height));

            if(arrangerModel.Layout == ArrangerLayout.Tiled)
                arrangerNode.Add(new XAttribute("layout", "tiled"));
            else if (arrangerModel.Layout == ArrangerLayout.Single)
                arrangerNode.Add(new XAttribute("layout", "single"));

            if (arrangerModel.ColorType == PixelColorType.Indexed)
                arrangerNode.Add(new XAttribute("color", "indexed"));
            else if (arrangerModel.ColorType == PixelColorType.Direct)
                arrangerNode.Add(new XAttribute("color", "direct"));

            arrangerNode.Add(new XAttribute("defaultcodec", defaultCodec));
            arrangerNode.Add(new XAttribute("defaultdatafile", defaultFile));

            for(int y = 0; y < arrangerModel.ArrangerElementSize.Height; y++)
            {
                for(int x = 0; x < arrangerModel.ArrangerElementSize.Width; x++)
                {
                    var el = arrangerModel.ElementGrid[x, y];

                    if (el.CodecName == "Blank Indexed" || el.CodecName == "Blank Direct")
                        continue;

                    var elNode = new XElement("element");
                    elNode.Add(new XAttribute("fileoffset", $"{el.FileAddress.FileOffset:X}"));
                    elNode.Add(new XAttribute("posx", el.PositionX));
                    elNode.Add(new XAttribute("posy", el.PositionY));

                    if(el.CodecName != defaultCodec)
                        elNode.Add(new XAttribute("codec", el.CodecName));

                    if(el.DataFileKey != defaultFile)
                        elNode.Add(new XAttribute("datafile", el.DataFileKey));

                    if(!string.IsNullOrWhiteSpace(el.PaletteKey))
                        elNode.Add(new XAttribute("palette", el.PaletteKey));

                    arrangerNode.Add(elNode);
                }
            }

            return arrangerNode;
        }
    }
}
