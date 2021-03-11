using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using ImageMagitek.Colors;
using Monaco.PathTree;

namespace ImageMagitek.Project.Serialization
{
    //public class XmlGameDescriptorWriter : IGameDescriptorWriter
    //{
    //    public string DescriptorVersion => "0.8";
    //    private readonly List<IProjectResource> _globalResources;
    //    private readonly Palette _globalDefaultPalette;
    //    private string _baseDirectory;

    //    public XmlGameDescriptorWriter() : this(Enumerable.Empty<IProjectResource>())
    //    {
    //    }

    //    public XmlGameDescriptorWriter(IEnumerable<IProjectResource> globalResources)
    //    {
    //        _globalResources = globalResources.ToList();
    //        _globalDefaultPalette = globalResources.OfType<Palette>().FirstOrDefault();
    //    }

    //    public MagitekResult WriteProject(ProjectTree tree, string fileName)
    //    {
    //        if (tree is null)
    //            throw new ArgumentNullException($"{nameof(WriteProject)} property '{nameof(tree)}' was null");

    //        if (string.IsNullOrWhiteSpace(fileName))
    //            throw new ArgumentException($"{nameof(WriteProject)} property '{nameof(fileName)}' was null or empty");

    //        _baseDirectory = Path.GetDirectoryName(Path.GetFullPath(fileName));

    //        var xmlRoot = new XElement("gdf");
    //        xmlRoot.Add(new XAttribute("version", DescriptorVersion));

    //        var modelTree = BuildModelTree(tree);

    //        foreach(var node in modelTree.EnumerateDepthFirst())
    //        {
    //            XElement element = node.Item switch
    //            {
    //                ResourceFolderModel folderModel => Serialize(folderModel),
    //                DataFileModel dataFileModel => Serialize(dataFileModel),
    //                PaletteModel paletteModel => Serialize(paletteModel),
    //                ScatteredArrangerModel arrangerModel => Serialize(arrangerModel),
    //                ImageProjectModel projectModel => Serialize(projectModel),
    //                _ => throw new InvalidOperationException($"{nameof(WriteProject)}: unexpected node of type '{node.Item.GetType()}'"),
    //            };

    //            AddResourceToXmlTree(xmlRoot, element, node.Paths.ToArray());
    //        }

    //        var xws = new XmlWriterSettings
    //        {
    //            Indent = true,
    //            IndentChars = "\t"
    //        };

    //        try
    //        {
    //            using var fs = new FileStream(fileName, FileMode.Create);
    //            using var xw = XmlWriter.Create(fs, xws);
    //            xmlRoot.Save(xw);

    //            return MagitekResult.SuccessResult;
    //        }
    //        catch (Exception ex)
    //        {
    //            return new MagitekResult.Failed($"{ex.Message}");
    //        }
    //    }

    //    private ProjectModelTree BuildModelTree(ProjectTree projectTree)
    //    {
    //        var resourceResolver = new Dictionary<IProjectResource, string>();
    //        foreach (var node in projectTree.EnumerateDepthFirst())
    //            resourceResolver.Add(node.Item, node.PathKey);

    //        var projectModel = projectTree.Project.MapToModel();
    //        var root = new ResourceModelNode(projectModel.Name, projectModel);
    //        var modelTree = new ProjectModelTree(root);

    //        foreach (var node in projectTree.EnumerateDepthFirst().Where(x => x.Item.ShouldBeSerialized && x.Item is ResourceFolder))
    //        {
    //            var folderModel = (node.Item as ResourceFolder).MapToModel();
    //            modelTree.AddItemAsPath(node.PathKey, folderModel);
    //        }

    //        foreach (var node in projectTree.EnumerateDepthFirst().Where(x => x.Item.ShouldBeSerialized))
    //        {
    //            switch (node.Item)
    //            {
    //                case ResourceFolder folder:
    //                    break;
    //                case DataFile dataFile:
    //                    var dataFileModel = dataFile.MapToModel();
    //                    modelTree.AddItemAsPath(node.PathKey, dataFileModel);
    //                    break;
    //                case Palette palette:
    //                    var paletteModel = palette.MapToModel();
    //                    paletteModel.DataFileKey = resourceResolver[palette.DataFile];
    //                    modelTree.AddItemAsPath(node.PathKey, paletteModel);
    //                    break;
    //                case ScatteredArranger arranger:
    //                    var arrangerModel = arranger.MapToModel();
    //                    for (int x = 0; x < arrangerModel.ArrangerElementSize.Width; x++)
    //                    {
    //                        for (int y = 0; y < arrangerModel.ArrangerElementSize.Height; y++)
    //                        {
    //                            if (arranger.GetElement(x, y) is ArrangerElement element)
    //                            {
    //                                var elementModel = element.MapToModel(x, y);

    //                                if (element.DataFile is object)
    //                                    elementModel.DataFileKey = resourceResolver[element.DataFile];
    //                                else
    //                                    continue;

    //                                if (!resourceResolver.TryGetValue(element.Palette, out var paletteKey))
    //                                {
    //                                    paletteKey = _globalResources.OfType<Palette>()
    //                                        .FirstOrDefault(x => ReferenceEquals(element.Palette, x))?.Name;
    //                                }

    //                                elementModel.PaletteKey = paletteKey;
    //                                arrangerModel.ElementGrid[x, y] = elementModel;
    //                            }
    //                        }
    //                    }

    //                    modelTree.AddItemAsPath(node.PathKey, arrangerModel);
    //                    break;
    //            }
    //        }

    //        return modelTree;
    //    }

    //    private void AddResourceToXmlTree(XElement projectNode, XElement resourceNode, string[] resourcePaths)
    //    {
    //        var nodeVisitor = projectNode;

    //        foreach(var path in resourcePaths.Take(resourcePaths.Length - 1))
    //        {
    //            nodeVisitor = nodeVisitor.Elements().Where(x => (string) x.Attribute("name") == path).FirstOrDefault();
    //            if (nodeVisitor is null)
    //                throw new KeyNotFoundException($"{nameof(AddResourceToXmlTree)}: node with path '{path}' not found");
    //        }

    //        nodeVisitor.Add(resourceNode);
    //    }

    //    private XElement Serialize(ImageProjectModel projectModel)
    //    {
    //        var element = new XElement("project");
    //        element.Add(new XAttribute("name", projectModel.Name));

    //        if (!string.IsNullOrEmpty(projectModel.Root))
    //            element.Add(new XAttribute("root", projectModel.Root));

    //        return element;
    //    }

    //    private XElement Serialize(ResourceFolderModel folderModel)
    //    {
    //        return new XElement("folder", new XAttribute("name", folderModel.Name));
    //    }

    //    private XElement Serialize(DataFileModel dataFileModel)
    //    {
    //        var element = new XElement("datafile");
    //        element.Add(new XAttribute("name", dataFileModel.Name));

    //        var relativePath = Path.GetRelativePath(_baseDirectory, dataFileModel.Location);
    //        element.Add(new XAttribute("location", relativePath));
    //        return element;
    //    }

    //    private XElement Serialize(PaletteModel paletteModel)
    //    {
    //        var element = new XElement("palette");
    //        element.Add(new XAttribute("name", paletteModel.Name));
    //        element.Add(new XAttribute("fileoffset", $"{paletteModel.FileAddress.FileOffset:X}"));
    //        element.Add(new XAttribute("datafile", paletteModel.DataFileKey));
    //        element.Add(new XAttribute("color", paletteModel.ColorModel.ToString()));
    //        element.Add(new XAttribute("entries", paletteModel.Entries));
    //        element.Add(new XAttribute("zeroindextransparent", paletteModel.ZeroIndexTransparent));

    //        return element;
    //    }

    //    private XElement Serialize(ScatteredArrangerModel arrangerModel)
    //    {
    //        var mostUsedCodecName = arrangerModel.FindMostFrequentPropertyValue("CodecName");
    //        var mostUsedFileKey = arrangerModel.FindMostFrequentPropertyValue("DataFileKey");
    //        string mostUsedPaletteKey = arrangerModel.FindMostFrequentPropertyValue("PaletteKey");

    //        var arrangerNode = new XElement("arranger");
    //        arrangerNode.Add(new XAttribute("name", arrangerModel.Name));
    //        arrangerNode.Add(new XAttribute("elementsx", arrangerModel.ArrangerElementSize.Width));
    //        arrangerNode.Add(new XAttribute("elementsy", arrangerModel.ArrangerElementSize.Height));
    //        arrangerNode.Add(new XAttribute("width", arrangerModel.ElementPixelSize.Width));
    //        arrangerNode.Add(new XAttribute("height", arrangerModel.ElementPixelSize.Height));

    //        if(arrangerModel.Layout == ArrangerLayout.Tiled)
    //            arrangerNode.Add(new XAttribute("layout", "tiled"));
    //        else if (arrangerModel.Layout == ArrangerLayout.Single)
    //            arrangerNode.Add(new XAttribute("layout", "single"));

    //        if (arrangerModel.ColorType == PixelColorType.Indexed)
    //            arrangerNode.Add(new XAttribute("color", "indexed"));
    //        else if (arrangerModel.ColorType == PixelColorType.Direct)
    //            arrangerNode.Add(new XAttribute("color", "direct"));

    //        arrangerNode.Add(new XAttribute("defaultcodec", mostUsedCodecName ?? ""));
    //        arrangerNode.Add(new XAttribute("defaultdatafile", mostUsedFileKey ?? ""));
    //        arrangerNode.Add(new XAttribute("defaultpalette", mostUsedPaletteKey ?? ""));

    //        for (int y = 0; y < arrangerModel.ArrangerElementSize.Height; y++)
    //        {
    //            for(int x = 0; x < arrangerModel.ArrangerElementSize.Width; x++)
    //            {
    //                var el = arrangerModel.ElementGrid[x, y];

    //                if (el is null)
    //                    continue;

    //                var elNode = new XElement("element");
    //                elNode.Add(new XAttribute("fileoffset", $"{el.FileAddress.FileOffset:X}"));
    //                elNode.Add(new XAttribute("posx", el.PositionX));
    //                elNode.Add(new XAttribute("posy", el.PositionY));

    //                if (el.FileAddress.BitOffset != 0)
    //                    elNode.Add(new XAttribute("bitoffset", el.FileAddress.BitOffset));

    //                if(el.CodecName != mostUsedCodecName)
    //                    elNode.Add(new XAttribute("codec", el.CodecName));

    //                if(el.DataFileKey != mostUsedFileKey)
    //                    elNode.Add(new XAttribute("datafile", el.DataFileKey));

    //                if (el.PaletteKey != mostUsedPaletteKey)
    //                    elNode.Add(new XAttribute("palette", el.PaletteKey));

    //                arrangerNode.Add(elNode);
    //            }
    //        }

    //        return arrangerNode;
    //    }
    //}
}
