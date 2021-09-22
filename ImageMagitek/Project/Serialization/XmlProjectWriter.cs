using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Utility;
using Monaco.PathTree;

namespace ImageMagitek.Project.Serialization
{
    public sealed class XmlProjectWriter : IProjectWriter
    {
        public string Version => "0.9";
        private readonly List<IProjectResource> _globalResources;
        private readonly Palette _globalDefaultPalette;
        private readonly ProjectTree _tree;
        private readonly IColorFactory _colorFactory;
        private string _baseDirectory;

        private HashSet<string> _activeBackupFiles;

        public XmlProjectWriter(ProjectTree tree, IColorFactory colorFactory, IEnumerable<IProjectResource> globalResources)
        {
            if (tree is null)
                throw new ArgumentNullException($"{nameof(WriteProject)} parameter '{nameof(tree)}' was null");

            _tree = tree;
            _colorFactory = colorFactory;
            _globalResources = globalResources.ToList();
            _globalDefaultPalette = globalResources.OfType<Palette>().FirstOrDefault();
            _baseDirectory = Path.GetDirectoryName(Path.GetFullPath(tree.Root.DiskLocation));
        }

        /// <summary>
        /// Writes all modified project resources contained in a ProjectTree to disk and updates the persistence models
        /// while accounting for stale references
        /// </summary>
        /// <param name="projectFileName">Filename to write to</param>
        /// <returns></returns>
        public MagitekResult WriteProject(string projectFileName)
        {
            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(WriteProject)} property '{nameof(projectFileName)}' was null or empty");

            _baseDirectory = Path.GetDirectoryName(Path.GetFullPath(projectFileName));
            _activeBackupFiles = new HashSet<string>();

            return TrySerializeProjectTree(_tree).Match<MagitekResult>(
                success => MagitekResult.SuccessResult,
                failed => new MagitekResult.Failed(failed.Reasons.First()));
        }

        /// <summary>
        /// Serializes the resource to a string
        /// </summary>
        /// <param name="resourceNode"></param>
        /// <returns></returns>
        public string SerializeResource(ResourceNode resourceNode)
        {
            var resourceMap = CreateResourceMap();

            if (resourceNode is ProjectNode projectNode)
            {
                var model = (projectNode.Item as ImageProject).MapToModel();
                return Stringify(Serialize(model));
            }
            else if (resourceNode is DataFileNode dfNode)
            {
                var model = (dfNode.Item as DataFile).MapToModel();
                return Stringify(Serialize(model));
            }
            else if (resourceNode is PaletteNode palNode)
            {
                var model = (palNode.Item as Palette).MapToModel(resourceMap, _colorFactory);
                return Stringify(Serialize(model));
            }
            else if (resourceNode is ArrangerNode arrangerNode)
            {
                var model = (arrangerNode.Item as ScatteredArranger).MapToModel(resourceMap);
                return Stringify(Serialize(model));
            }
            else
                return null;
        }

        /// <summary>
        /// Writes a single resource, updating its model, but does not update stale resources in the tree
        /// </summary>
        /// <param name="resourceNode"></param>
        /// <param name="alwaysOverwrite"></param>
        /// <returns></returns>
        public MagitekResult WriteResource(ResourceNode resourceNode, bool alwaysOverwrite)
        {
            string contents;
            ResourceModel currentModel;
            ResourceModel diskModel;
            var resourceMap = CreateResourceMap();

            if (resourceNode is ProjectNode projectNode)
            {
                var model = (projectNode.Item as ImageProject).MapToModel();
                contents = Stringify(Serialize(model));
                currentModel = model;
                diskModel = projectNode.Model;
            }
            else if (resourceNode is DataFileNode dfNode)
            {
                var model = (dfNode.Item as DataFile).MapToModel();
                contents = Stringify(Serialize(model));
                currentModel = model;
                diskModel = dfNode.Model;
            }
            else if (resourceNode is PaletteNode palNode)
            {
                var model = (palNode.Item as Palette).MapToModel(resourceMap, _colorFactory);
                contents = Stringify(Serialize(model));
                currentModel = model;
                diskModel = palNode.Model;
            }
            else if (resourceNode is ArrangerNode arrangerNode)
            {
                var model = (arrangerNode.Item as ScatteredArranger).MapToModel(resourceMap);
                contents = Stringify(Serialize(model));
                currentModel = model;
                diskModel = arrangerNode.Model;
            }
            else
            {
                return new MagitekResult.Failed($"{nameof(WriteResource)} cannot write resource of type '{resourceNode}'");
            }

            if (currentModel.ResourceEquals(diskModel) == false || alwaysOverwrite)
            {
                var actions = new[] { (CreateWriteAction(currentModel, resourceNode.DiskLocation), resourceNode, currentModel) };

                return RunTransactions(actions).Match<MagitekResult>(
                    success => MagitekResult.SuccessResult,
                    failed => new MagitekResult.Failed(failed.Reasons.First()));
            }

            return MagitekResult.SuccessResult;
        }

        private MagitekResults TrySerializeProjectTree(ProjectTree tree)
        {
            var actions = new List<(BackupFileAndOverwriteExistingTransaction transaction, ResourceNode node, ResourceModel model)>();

            foreach (var node in tree.EnumerateDepthFirst().Where(x => x is not ResourceFolderNode))
            {
                ResourceModel currentModel;
                ResourceModel diskModel;
                var resourceMap = CreateResourceMap();

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
                    var model = pal.MapToModel(resourceMap, _colorFactory);
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

                var location = ResourceFileLocator.LocateByParent(tree, node.Parent, node);

                if (!currentModel.ResourceEquals(diskModel) || node.DiskLocation != location)
                {
                    actions.Add((CreateWriteAction(currentModel, location), node, currentModel));
                }
            }

            return RunTransactions(actions);
        }

        private MagitekResults RunTransactions(IList<(BackupFileAndOverwriteExistingTransaction transaction, ResourceNode node, ResourceModel model)> actions)
        {
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
                    {
                        throw new InvalidOperationException($"Serializing project node with unexpected type '{action.node.GetType()}' is not supported");
                    }

                    action.node.DiskLocation = action.transaction.PrimaryFileName;
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

            return new BackupFileAndOverwriteExistingTransaction(diskLocation, Stringify(element));
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
            element.Add(new XAttribute("version", Version));

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

            var relativePath = Path.GetRelativePath(_baseDirectory, dataFileModel.Location);
            element.Add(new XAttribute("location", relativePath));
            return element;
        }

        private XElement Serialize(PaletteModel paletteModel)
        {
            var element = new XElement("palette");
            element.Add(new XAttribute("datafile", paletteModel.DataFileKey));
            element.Add(new XAttribute("color", paletteModel.ColorModel.ToString()));
            element.Add(new XAttribute("zeroindextransparent", paletteModel.ZeroIndexTransparent));

            foreach (var source in paletteModel.ColorSources)
            {
                if (source is FileColorSourceModel fileSource)
                {
                    var fileElement = new XElement("filesource");
                    fileElement.Add(new XAttribute("fileoffset", $"{fileSource.FileAddress.FileOffset:X}"));
                    fileElement.Add(new XAttribute("entries", fileSource.Entries));

                    if (fileSource.Endian == Endian.Big)
                        fileElement.Add(new XAttribute("endian", "big"));

                    element.Add(fileElement);
                }
                else if (source is ProjectNativeColorSourceModel nativeSource)
                {
                    var nativeElement = new XElement("nativecolor");
                    var hexColor = _colorFactory.ToHexString(nativeSource.Value);
                    nativeElement.Add(new XAttribute("value", hexColor));
                    element.Add(nativeElement);
                }
                else if (source is ProjectForeignColorSourceModel foreignSource)
                {
                    var foreignElement = new XElement("foreigncolor");
                    var hexColor = _colorFactory.ToHexString(foreignSource.Value);
                    foreignElement.Add(new XAttribute("value", hexColor));
                    element.Add(foreignElement);
                }
            }

            return element;
        }

        private XElement Serialize(ScatteredArrangerModel arrangerModel)
        {
            var mostUsedCodecName = arrangerModel.FindMostFrequentPropertyValue("CodecName");
            var mostUsedFileKey = arrangerModel.FindMostFrequentPropertyValue("DataFileKey");
            string mostUsedPaletteKey = arrangerModel.FindMostFrequentPropertyValue("PaletteKey");

            var arrangerNode = new XElement("arranger");
            arrangerNode.Add(new XAttribute("elementsx", arrangerModel.ArrangerElementSize.Width));
            arrangerNode.Add(new XAttribute("elementsy", arrangerModel.ArrangerElementSize.Height));
            arrangerNode.Add(new XAttribute("width", arrangerModel.ElementPixelSize.Width));
            arrangerNode.Add(new XAttribute("height", arrangerModel.ElementPixelSize.Height));

            if (arrangerModel.Layout == ElementLayout.Tiled)
                arrangerNode.Add(new XAttribute("layout", "tiled"));
            else if (arrangerModel.Layout == ElementLayout.Single)
                arrangerNode.Add(new XAttribute("layout", "single"));

            if (arrangerModel.ColorType == PixelColorType.Indexed)
                arrangerNode.Add(new XAttribute("color", "indexed"));
            else if (arrangerModel.ColorType == PixelColorType.Direct)
                arrangerNode.Add(new XAttribute("color", "direct"));

            arrangerNode.Add(new XAttribute("defaultcodec", mostUsedCodecName ?? ""));
            arrangerNode.Add(new XAttribute("defaultdatafile", mostUsedFileKey ?? ""));

            if (arrangerModel.ColorType == PixelColorType.Indexed)
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

                    if (el.PaletteKey != mostUsedPaletteKey && arrangerModel.ColorType == PixelColorType.Indexed)
                        elNode.Add(new XAttribute("palette", el.PaletteKey));

                    if (el.Mirror == MirrorOperation.Horizontal)
                        elNode.Add(new XAttribute("mirror", "horizontal"));
                    else if (el.Mirror == MirrorOperation.Vertical)
                        elNode.Add(new XAttribute("mirror", "vertical"));
                    else if (el.Mirror == MirrorOperation.Both)
                        elNode.Add(new XAttribute("mirror", "both"));

                    if (el.Rotation == RotationOperation.Left)
                        elNode.Add(new XAttribute("rotation", "left"));
                    else if (el.Rotation == RotationOperation.Right)
                        elNode.Add(new XAttribute("rotation", "right"));
                    else if (el.Rotation == RotationOperation.Turn)
                        elNode.Add(new XAttribute("rotation", "turn"));

                    arrangerNode.Add(elNode);
                }
            }

            return arrangerNode;
        }

        private string Stringify(XElement resourceElement)
        {
            var xws = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "\t",
            };

            using TextWriter writer = new Utf8StringWriter();
            using var xw = XmlWriter.Create(writer, xws);

            resourceElement.Save(xw);
            xw.Flush();

            return writer.ToString();
        }

        private Dictionary<IProjectResource, string> CreateResourceMap()
        {
            var resourceMap = new Dictionary<IProjectResource, string>();

            foreach (var resource in _globalResources)
                resourceMap.Add(resource, resource.Name);

            foreach (var node in _tree.EnumerateDepthFirst().Where(x => x is not ResourceFolderNode))
                resourceMap.Add(node.Item, _tree.CreatePathKey(node));

            return resourceMap;
        }
    }
}
