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

            return TrySerializeProjectTree(tree).Match<MagitekResult>(
                success => MagitekResult.SuccessResult,
                failed => new MagitekResult.Failed(failed.Reasons.First()));
        }

        private MagitekResults TrySerializeProjectTree(ProjectTree tree)
        {
            var actions = new List<(BackupFileAndOverwriteExistingTransaction transaction, ResourceNode node, ResourceModel model)>();
            var resourceMap = new Dictionary<IProjectResource, string>();

            foreach (var resource in _globalResources)
                resourceMap.Add(resource, resource.Name);

            foreach (var node in tree.EnumerateDepthFirst().Where(x => x is not ResourceFolderNode))
                resourceMap.Add(node.Item, tree.CreatePathKey(node));

            foreach (var node in tree.EnumerateDepthFirst().Where(x => x is not ResourceFolderNode))
            {
                ResourceModel currentModel;
                ResourceModel diskModel;

                if (node is ProjectNode projectNode)
                {
                    var model = (projectNode.Item as ImageProject).MapToModel();
                    currentModel = model;
                    diskModel = projectNode.Model;
                }
                else if (node is DataFileNode dfNode)
                {
                    var model = (dfNode.Item as DataFile).MapToModel();
                    currentModel = model;
                    diskModel = dfNode.Model;
                }
                else if (node is PaletteNode paletteNode)
                {
                    var pal = paletteNode.Item as Palette;
                    var model = pal.MapToModel(resourceMap);
                    currentModel = model;

                    diskModel = paletteNode.Model;
                }
                else if (node is ArrangerNode arrangerNode)
                {
                    var arranger = arrangerNode.Item as ScatteredArranger;
                    var model = arranger.MapToModel(resourceMap);
                    currentModel = model;

                    diskModel = arrangerNode.Model;
                }
                else
                    throw new InvalidOperationException($"Serializing project node with unexpected type '{node.GetType()}' is not supported");

                if (!currentModel.ResourceEquals(diskModel))
                {
                    actions.Add((CreateWriteAction(currentModel, node.DiskLocation), node, currentModel));
                }
            }

            var transaction = new FileSetWriteTransaction(actions.Select(x => x.transaction));
            var result = transaction.Transact();

            if (result.HasSucceeded)
            {
                foreach (var action in actions)
                {
                    if (action.node is ProjectNode projectNode)
                    {
                        projectNode.Model = action.model as ImageProjectModel;
                    }
                    else if (action.node is DataFileNode dfNode)
                    {
                        dfNode.Model = action.model as DataFileModel;
                    }
                    else if (action.node is PaletteNode paletteNode)
                    {
                        paletteNode.Model = action.model as PaletteModel;
                    }
                    else if (action.node is ArrangerNode arrangerNode)
                    {
                        arrangerNode.Model = action.model as ScatteredArrangerModel;
                    }
                    else
                        throw new InvalidOperationException($"Serializing project node with unexpected type '{action.node.GetType()}' is not supported");
                }
            }

            return result;
        }

        private BackupFileAndOverwriteExistingTransaction CreateWriteAction(ResourceModel model, string diskLocation)
        {
            XElement element = model switch
            {
                ResourceFolderModel folderModel => Serialize(folderModel),
                DataFileModel dataFileModel => Serialize(dataFileModel),
                PaletteModel paletteModel => Serialize(paletteModel),
                ScatteredArrangerModel arrangerModel => Serialize(arrangerModel),
                ImageProjectModel projectModel => Serialize(projectModel),
                _ => throw new InvalidOperationException($"{nameof(WriteProject)}: unexpected resource model of type '{model.GetType()}'"),
            };

            var xws = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "\t"
            };

            var sb = new StringBuilder();
            using var xw = XmlWriter.Create(sb, xws);
            element.Save(xw);
            xw.Flush();

            return new BackupFileAndOverwriteExistingTransaction(diskLocation, sb.ToString());
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
