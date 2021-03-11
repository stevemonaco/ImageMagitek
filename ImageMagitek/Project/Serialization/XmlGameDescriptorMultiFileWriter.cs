using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Utility;
using Monaco.PathTree;

namespace ImageMagitek.Project.Serialization
{
    public class XmlGameDescriptorMultiFileWriter : IGameDescriptorWriter
    {
        public string DescriptorVersion => "0.9";
        private readonly List<IProjectResource> _globalResources;
        private readonly Palette _globalDefaultPalette;
        private string _baseDirectory;

        private HashSet<string> _activeBackupFiles;

        public XmlGameDescriptorMultiFileWriter(IEnumerable<IProjectResource> globalResources)
        {
            _globalResources = globalResources.ToList();
            _globalDefaultPalette = globalResources.OfType<Palette>().FirstOrDefault();
        }

        /// <summary>
        /// Writes all project resources contained in a ProjectTree to disk
        /// </summary>
        /// <param name="tree">Tree to be saved</param>
        /// <param name="projectFileName">Filename to write</param>
        /// <returns></returns>
        public MagitekResult WriteProject(ProjectTree tree, string projectFileName)
        {
            if (tree is null)
                throw new ArgumentNullException($"{nameof(WriteProject)} property '{nameof(tree)}' was null");

            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(WriteProject)} property '{nameof(projectFileName)}' was null or empty");

            _baseDirectory = Path.GetDirectoryName(Path.GetFullPath(projectFileName));
            _activeBackupFiles = new HashSet<string>();

            var modelTree = BuildModelTree(tree);

            return TrySerializeModelTree(tree, modelTree).Match<MagitekResult>(
                success => MagitekResult.SuccessResult,
                failed => new MagitekResult.Failed(failed.Reasons.First()));
        }

        private MagitekResults TrySerializeModelTree(ProjectTree projectTree, ProjectModelTree modelTree)
        {
            var actions = new List<BackupFileAndOverwriteExistingTransaction>();

            foreach (var modelNode in modelTree.EnumerateDepthFirst().Where(x => x.Item is not ResourceFolderModel))
            {
                var modelKey = modelTree.CreatePathKey(modelNode);
                projectTree.TryGetNode<ProjectNode>(modelKey, out var projectNode);

                if (modelNode.Item.ResourceEquals(projectNode.Model))
                    continue;

                var diskLocation = LocateResourceOnDisk(projectNode);
                 actions.Add(CreateWriteAction(modelNode, diskLocation));
            }

            var transaction = new FileSetWriteTransaction(actions);

            return transaction.Transact();
        }

        private BackupFileAndOverwriteExistingTransaction CreateWriteAction(ResourceModelNode modelNode, string diskLocation)
        {
            XElement element = modelNode.Item switch
            {
                ResourceFolderModel folderModel => Serialize(folderModel),
                DataFileModel dataFileModel => Serialize(dataFileModel),
                PaletteModel paletteModel => Serialize(paletteModel),
                ScatteredArrangerModel arrangerModel => Serialize(arrangerModel),
                ImageProjectModel projectModel => Serialize(projectModel),
                _ => throw new InvalidOperationException($"{nameof(WriteProject)}: unexpected node of type '{modelNode.Item.GetType()}'"),
            };

            var xws = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "\t"
            };

            var sb = new StringBuilder();
            using var xw = XmlWriter.Create(sb, xws);
            element.Save(xw);

            var action = new BackupFileAndOverwriteExistingTransaction(diskLocation, sb.ToString());
            return action;
        }

        private ProjectModelTree BuildModelTree(ProjectTree projectTree)
        {
            var resourceResolver = new Dictionary<IProjectResource, string>();
            foreach (var node in projectTree.EnumerateDepthFirst())
                resourceResolver.Add(node.Item, projectTree.CreatePathKey(node));

            var projectModel = projectTree.Project.MapToModel();
            var root = new ResourceModelNode(projectModel.Name, projectModel);
            var modelTree = new ProjectModelTree(root);

            foreach (var projectNode in projectTree.EnumerateDepthFirst().Where(x => x.Item.ShouldBeSerialized && x.Item is ResourceFolder))
            {
                var folderModel = (projectNode.Item as ResourceFolder).MapToModel();
                var modelNode = new ResourceModelNode(projectNode.Name, folderModel);
                modelTree.AttachNodeAsPath(projectTree.CreatePathKey(projectNode), modelNode);
            }

            foreach (var node in projectTree.EnumerateDepthFirst().Where(x => x.Item.ShouldBeSerialized))
            {
                switch (node.Item)
                {
                    case ResourceFolder folder:
                        break;
                    case DataFile dataFile:
                        var dataFileModel = dataFile.MapToModel();
                        var fileNode = new ResourceModelNode(node.Name, dataFileModel);
                        modelTree.AttachNodeAsPath(projectTree.CreatePathKey(node), fileNode);
                        break;
                    case Palette palette:
                        var paletteModel = palette.MapToModel();
                        paletteModel.DataFileKey = resourceResolver[palette.DataFile];
                        var paletteNode = new ResourceModelNode(node.Name, paletteModel);
                        modelTree.AttachNodeAsPath(projectTree.CreatePathKey(node), paletteNode);
                        break;
                    case ScatteredArranger arranger:
                        var arrangerModel = arranger.MapToModel();
                        for (int x = 0; x < arrangerModel.ArrangerElementSize.Width; x++)
                        {
                            for (int y = 0; y < arrangerModel.ArrangerElementSize.Height; y++)
                            {
                                if (arranger.GetElement(x, y) is ArrangerElement element)
                                {
                                    var elementModel = element.MapToModel(x, y);

                                    if (element.DataFile is object)
                                        elementModel.DataFileKey = resourceResolver[element.DataFile];
                                    else
                                        continue;

                                    if (!resourceResolver.TryGetValue(element.Palette, out var paletteKey))
                                    {
                                        paletteKey = _globalResources.OfType<Palette>()
                                            .FirstOrDefault(x => ReferenceEquals(element.Palette, x))?.Name;
                                    }

                                    elementModel.PaletteKey = paletteKey;
                                    arrangerModel.ElementGrid[x, y] = elementModel;
                                }
                            }
                        }

                        var arrangerNode = new ResourceModelNode(node.Name, arrangerModel);
                        modelTree.AttachNodeAsPath(projectTree.CreatePathKey(node), arrangerNode);
                        break;
                }
            }

            return modelTree;
        }

        private void AddResourceToXmlTree(XElement projectNode, XElement resourceNode, string[] resourcePaths)
        {
            var nodeVisitor = projectNode;

            foreach (var path in resourcePaths.Take(resourcePaths.Length - 1))
            {
                nodeVisitor = nodeVisitor.Elements().Where(x => (string)x.Attribute("name") == path).FirstOrDefault();
                if (nodeVisitor is null)
                    throw new KeyNotFoundException($"{nameof(AddResourceToXmlTree)}: node with path '{path}' not found");
            }

            nodeVisitor.Add(resourceNode);
        }

        private XElement Serialize(ImageProjectModel projectModel)
        {
            var element = new XElement("project");
            element.Add(new XAttribute("name", projectModel.Name));
            element.Add(new XAttribute("version", DescriptorVersion));

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

            var relativePath = Path.GetRelativePath(_baseDirectory, dataFileModel.Location);
            element.Add(new XAttribute("location", relativePath));
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
            var mostUsedCodecName = arrangerModel.FindMostFrequentPropertyValue("CodecName");
            var mostUsedFileKey = arrangerModel.FindMostFrequentPropertyValue("DataFileKey");
            string mostUsedPaletteKey = arrangerModel.FindMostFrequentPropertyValue("PaletteKey");

            var arrangerNode = new XElement("arranger");
            arrangerNode.Add(new XAttribute("name", arrangerModel.Name));
            arrangerNode.Add(new XAttribute("elementsx", arrangerModel.ArrangerElementSize.Width));
            arrangerNode.Add(new XAttribute("elementsy", arrangerModel.ArrangerElementSize.Height));
            arrangerNode.Add(new XAttribute("width", arrangerModel.ElementPixelSize.Width));
            arrangerNode.Add(new XAttribute("height", arrangerModel.ElementPixelSize.Height));

            if (arrangerModel.Layout == ArrangerLayout.Tiled)
                arrangerNode.Add(new XAttribute("layout", "tiled"));
            else if (arrangerModel.Layout == ArrangerLayout.Single)
                arrangerNode.Add(new XAttribute("layout", "single"));

            if (arrangerModel.ColorType == PixelColorType.Indexed)
                arrangerNode.Add(new XAttribute("color", "indexed"));
            else if (arrangerModel.ColorType == PixelColorType.Direct)
                arrangerNode.Add(new XAttribute("color", "direct"));

            arrangerNode.Add(new XAttribute("defaultcodec", mostUsedCodecName ?? ""));
            arrangerNode.Add(new XAttribute("defaultdatafile", mostUsedFileKey ?? ""));
            arrangerNode.Add(new XAttribute("defaultpalette", mostUsedPaletteKey ?? ""));

            for (int y = 0; y < arrangerModel.ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < arrangerModel.ArrangerElementSize.Width; x++)
                {
                    var el = arrangerModel.ElementGrid[x, y];

                    if (el is null)
                        continue;

                    var elNode = new XElement("element");
                    elNode.Add(new XAttribute("fileoffset", $"{el.FileAddress.FileOffset:X}"));
                    elNode.Add(new XAttribute("posx", el.PositionX));
                    elNode.Add(new XAttribute("posy", el.PositionY));

                    if (el.FileAddress.BitOffset != 0)
                        elNode.Add(new XAttribute("bitoffset", el.FileAddress.BitOffset));

                    if (el.CodecName != mostUsedCodecName)
                        elNode.Add(new XAttribute("codec", el.CodecName));

                    if (el.DataFileKey != mostUsedFileKey)
                        elNode.Add(new XAttribute("datafile", el.DataFileKey));

                    if (el.PaletteKey != mostUsedPaletteKey)
                        elNode.Add(new XAttribute("palette", el.PaletteKey));

                    arrangerNode.Add(elNode);
                }
            }

            return arrangerNode;
        }

        private string LocateResourceOnDisk(ResourceNode node)
        {
            var relativePath = string.Join
            (
                Path.DirectorySeparatorChar,
                node.SelfAndAncestors<ResourceNode, IProjectResource>().Select(x => x.Name).Reverse().Skip(1)
            );

            return Path.Join(_baseDirectory, $"{relativePath}.xml");
        }
    }
}
