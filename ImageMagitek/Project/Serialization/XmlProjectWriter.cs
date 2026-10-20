using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using CommunityToolkit.Diagnostics;
using ImageMagitek.Colors;
using ImageMagitek.Tilemaps;
using ImageMagitek.Utility;
using Monaco.PathTree;

namespace ImageMagitek.Project.Serialization;

public sealed class XmlProjectWriter : IProjectWriter
{
    private readonly List<IProjectResource> _globalResources;
    private readonly Palette _globalDefaultPalette;
    private readonly ProjectTree _tree;
    private readonly IColorFactory _colorFactory;
    private readonly string _resourceBaseDirectory;
    private string _journalDirectory;

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public XmlProjectWriter(ProjectTree tree, IColorFactory colorFactory, IEnumerable<IProjectResource> globalResources)
    {
        Guard.IsNotNull(tree);
        Guard.IsNotNull(tree.Root.DiskLocation);

        _tree = tree;
        _colorFactory = colorFactory;
        _globalResources = globalResources.ToList();
        _globalDefaultPalette = globalResources.OfType<Palette>().First();
        _journalDirectory = Path.GetDirectoryName(Path.GetFullPath(tree.Root.DiskLocation))!;
        _resourceBaseDirectory = tree.Root is ProjectNode projectNode ? Path.GetFullPath(string.IsNullOrEmpty(projectNode.BaseDirectory) ? "." : projectNode.BaseDirectory) : _journalDirectory;
    }

    /// <summary>
    /// Writes all modified project resources contained in a ProjectTree to disk and updates the persistence models
    /// while accounting for stale references
    /// </summary>
    /// <param name="projectFileName">Filename to write to</param>
    /// <returns></returns>
    public async Task<MagitekResult> WriteProjectAsync(string projectFileName)
    {
        if (string.IsNullOrWhiteSpace(projectFileName))
            throw new ArgumentException($"{nameof(WriteProjectAsync)} property '{nameof(projectFileName)}' was null or empty");

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _journalDirectory = Path.GetDirectoryName(Path.GetFullPath(projectFileName))!;

            var serializeResult = await TrySerializeProjectTreeAsync(_tree).ConfigureAwait(false);
            return serializeResult.Match<MagitekResult>(
                success => MagitekResult.SuccessResult,
                failed => new MagitekResult.Failed(failed.Reasons.First()));
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Serializes the resource to a string
    /// </summary>
    /// <param name="resourceNode"></param>
    /// <returns></returns>
    public string SerializeResource(ResourceNode resourceNode)
    {
        var resourceMap = CreateResourceMap();

        if (resourceNode is ProjectNode { Item: ImageProject project })
        {
            var model = project.MapToModel(ProjectFormat.Required(_tree));
            return Stringify(Serialize(model));
        }
        else if (resourceNode is DataFileNode { Item: FileDataSource df })
        {
            var model = df.MapToModel();
            return Stringify(Serialize(model));
        }
        else if (resourceNode is CompressedDataNode { Item: CompressedDataSource compressed })
        {
            var model = compressed.MapToModel(resourceMap);
            return Stringify(Serialize(model));
        }
        else if (resourceNode is TilemapNode { Item: TilemapArranger tilemap })
        {
            var model = tilemap.MapToModel(resourceMap);
            return Stringify(Serialize(model));
        }
        else if (resourceNode is PaletteNode { Item: Palette pal })
        {
            var model = pal.MapToModel(resourceMap, _colorFactory);
            if (FindUnwritableReason(model) is string reason)
                throw new InvalidOperationException(reason);

            return Stringify(Serialize(model));
        }
        else if (resourceNode is ArrangerNode { Item: ScatteredArranger arranger })
        {
            var model = arranger.MapToModel(resourceMap);
            return Stringify(Serialize(model));
        }
        else
            throw new NotSupportedException($"{nameof(ResourceNode)} of type '{resourceNode.GetType()}' containing item of type '{resourceNode.Item.GetType()}' is not supported");
    }

    /// <summary>
    /// Writes a single resource, updating its model, but does not update stale resources in the tree
    /// </summary>
    /// <param name="resourceNode"></param>
    /// <param name="alwaysOverwrite"></param>
    /// <returns></returns>
    public async Task<MagitekResult> WriteResourceAsync(ResourceNode resourceNode, bool alwaysOverwrite)
    {
        if (resourceNode.DiskLocation is null)
            return new MagitekResult.Failed($"Resource '{resourceNode.Name}' cannot be saved because '{nameof(resourceNode.DiskLocation)}' is null");

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            string contents;
            ResourceModel currentModel;
            var resourceMap = CreateResourceMap();

            if (resourceNode is ProjectNode { Item: ImageProject project })
            {
                var model = project.MapToModel(ProjectFormat.Required(_tree));
                contents = Stringify(Serialize(model));
                currentModel = model;
            }
            else if (resourceNode is DataFileNode { Item: FileDataSource df })
            {
                var model = df.MapToModel();
                contents = Stringify(Serialize(model));
                currentModel = model;
            }
            else if (resourceNode is CompressedDataNode { Item: CompressedDataSource compressed })
            {
                var model = compressed.MapToModel(resourceMap);
                contents = Stringify(Serialize(model));
                currentModel = model;
            }
            else if (resourceNode is TilemapNode { Item: TilemapArranger tilemap })
            {
                var model = tilemap.MapToModel(resourceMap);
                contents = Stringify(Serialize(model));
                currentModel = model;
            }
            else if (resourceNode is PaletteNode paletteNode)
            {
                var model = MapPaletteModel(paletteNode, resourceMap);
                if (FindUnwritableReason(model) is string reason)
                    return new MagitekResult.Failed(reason);

                contents = Stringify(Serialize(model));
                currentModel = model;
            }
            else if (resourceNode is ArrangerNode { Item: ScatteredArranger arranger })
            {
                var model = arranger.MapToModel(resourceMap);
                contents = Stringify(Serialize(model));
                currentModel = model;
            }
            else
            {
                return new MagitekResult.Failed($"{nameof(WriteResourceAsync)} cannot write resource of type '{resourceNode}'");
            }

            var diskModel = resourceNode.Model;

            if (currentModel.ResourceEquals(diskModel) == false || alwaysOverwrite)
            {
                var actions = new[] { (resourceNode.DiskLocation, contents, resourceNode, currentModel) };

                var transactionResult = await RunTransactionsAsync(actions).ConfigureAwait(false);
                return transactionResult.Match<MagitekResult>(
                    success => MagitekResult.SuccessResult,
                    failed => new MagitekResult.Failed(failed.Reasons.First()));
            }

            return MagitekResult.SuccessResult;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<MagitekResult> WriteModelsAsync(IReadOnlyList<(ResourceNode Node, ResourceModel Model)> writes)
    {
        foreach (var (node, _) in writes)
        {
            if (node.DiskLocation is null)
                return new MagitekResult.Failed($"Resource '{node.Name}' cannot be saved because '{nameof(node.DiskLocation)}' is null");
        }

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var actions = writes.Select(x => (x.Node.DiskLocation!, Stringify(SerializeModel(x.Model)), x.Node, x.Model));
            var transactionResult = await RunTransactionsAsync(actions).ConfigureAwait(false);
            return transactionResult.Match<MagitekResult>(
                success => MagitekResult.SuccessResult,
                failed => new MagitekResult.Failed(failed.Reasons.First()));
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<MagitekResults> TrySerializeProjectTreeAsync(ProjectTree tree)
    {
        var actions = new List<(string targetPath, string contents, ResourceNode node, ResourceModel model)>();

        foreach (var node in tree.EnumerateDepthFirst().Where(x => x is not ResourceFolderNode))
        {
            ResourceModel currentModel;
            ResourceModel? diskModel;
            var resourceMap = CreateResourceMap();

            if (node is ProjectNode projectNode)
            {
                var project = (ImageProject)projectNode.Item;
                currentModel = project.MapToModel(ProjectFormat.Required(tree));
                diskModel = projectNode.Model;
            }
            else if (node is DataFileNode dfNode)
            {
                var df = (FileDataSource)dfNode.Item;
                currentModel = df.MapToModel();
                diskModel = dfNode.Model;
            }
            else if (node is CompressedDataNode compressedNode)
            {
                currentModel = ((CompressedDataSource)compressedNode.Item).MapToModel(resourceMap);
                diskModel = compressedNode.Model;
            }
            else if (node is TilemapNode tilemapNode)
            {
                currentModel = ((TilemapArranger)tilemapNode.Item).MapToModel(resourceMap);
                diskModel = tilemapNode.Model;
            }
            else if (node is PaletteNode paletteNode)
            {
                var paletteModel = MapPaletteModel(paletteNode, resourceMap);
                if (FindUnwritableReason(paletteModel) is string reason)
                    return new MagitekResults.Failed([reason]);

                currentModel = paletteModel;
                diskModel = paletteNode.Model;
            }
            else if (node is ArrangerNode arrangerNode)
            {
                var arranger = (ScatteredArranger)arrangerNode.Item;
                currentModel = arranger.MapToModel(resourceMap);
                diskModel = arrangerNode.Model;
            }
            else
                throw new InvalidOperationException($"Serializing project node with unexpected type '{node.GetType()}' is not supported");

            var location = ResourceFileLocator.Locate(tree, node);

            if (!currentModel.ResourceEquals(diskModel) || node.DiskLocation != location)
            {
                var element = SerializeModel(currentModel);
                actions.Add((location, Stringify(element), node, currentModel));
            }
        }

        return await RunTransactionsAsync(actions).ConfigureAwait(false);
    }

    private async Task<MagitekResults> RunTransactionsAsync(IEnumerable<(string targetPath, string contents, ResourceNode node, ResourceModel model)> actions)
    {
        var actionList = actions.ToList();

        if (actionList.Count == 0)
            return MagitekResults.SuccessResults;

        var transaction = new WriteAheadLogTransaction(_journalDirectory);

        foreach (var action in actionList)
        {
            transaction.AddWriteFile(action.targetPath, action.contents);
        }

        var result = await transaction.ExecuteAsync().ConfigureAwait(false);

        if (result.HasSucceeded)
        {
            foreach (var action in actionList)
            {
                if (action.node is ProjectNode projectNode)
                {
                    projectNode.Model = (ImageProjectModel)action.model;
                }
                else if (action.node is DataFileNode dfNode)
                {
                    dfNode.Model = (DataFileModel)action.model;
                }
                else if (action.node is CompressedDataNode compressedNode)
                {
                    compressedNode.Model = (CompressedDataModel)action.model;
                }
                else if (action.node is TilemapNode tilemapNode)
                {
                    tilemapNode.Model = (TilemapModel)action.model;
                }
                else if (action.node is PaletteNode paletteNode)
                {
                    paletteNode.Model = (PaletteModel)action.model;
                }
                else if (action.node is ArrangerNode arrangerNode)
                {
                    arrangerNode.Model = (ScatteredArrangerModel)action.model;
                }
                else
                {
                    throw new InvalidOperationException($"Serializing project node with unexpected type '{action.node.GetType()}' is not supported");
                }

                action.node.DiskLocation = action.targetPath;
            }
        }

        return result;
    }

    private PaletteModel MapPaletteModel(PaletteNode node, Dictionary<IProjectResource, string> resourceMap) =>
        node.CommittedModel?.Invoke(resourceMap) ?? ((Palette)node.Item).MapToModel(resourceMap, _colorFactory);

    private string? FindUnwritableReason(PaletteModel model)
    {
        if (model.DataFileKey is null)
            return $"Palette '{model.Name}' cannot be saved because it has no data file";

        var foreignType = _colorFactory.CreateColor(model.ColorModel).GetType();
        var index = 0;

        foreach (var source in model.ColorSources)
        {
            if (source is ProjectForeignColorSourceModel foreign && foreign.Value.GetType() != foreignType)
                return $"Palette '{model.Name}' cannot be saved because color {index} is not a {model.ColorModel} color";

            index += source switch
            {
                FileColorSourceModel fileSource => fileSource.Entries,
                ScatteredColorSourceModel scatteredSource => scatteredSource.Entries,
                _ => 1
            };
        }

        if (index > Palette.MaxEntries)
            return $"Palette '{model.Name}' cannot be saved because it has {index} colors, more than the {Palette.MaxEntries} a palette can hold";

        return null;
    }

    private XElement SerializeModel(ResourceModel model)
    {
        return model switch
        {
            DataFileModel dataFileModel => Serialize(dataFileModel),
            CompressedDataModel compressedModel => Serialize(compressedModel),
            TilemapModel tilemapModel => Serialize(tilemapModel),
            PaletteModel paletteModel => Serialize(paletteModel),
            ScatteredArrangerModel arrangerModel => Serialize(arrangerModel),
            ImageProjectModel projectModel => Serialize(projectModel),
            _ => throw new InvalidOperationException($"{nameof(WriteProjectAsync)}: unexpected resource model of type '{model.GetType()}'"),
        };
    }

    private XElement Serialize(ImageProjectModel projectModel)
    {
        var element = new XElement("project");
        element.Add(new XAttribute("version", projectModel.Version.ToString(2)));

        if (!string.IsNullOrEmpty(projectModel.Root))
            element.Add(new XAttribute("root", projectModel.Root));

        return element;
    }

    private XElement Serialize(DataFileModel dataFileModel)
    {
        var element = new XElement("datafile");

        var relativePath = Path.GetRelativePath(_resourceBaseDirectory, dataFileModel.Location);
        element.Add(new XAttribute("location", relativePath));
        return element;
    }

    private static XElement Serialize(CompressedDataModel model)
    {
        var element = new XElement("compresseddata");
        element.Add(new XAttribute("parent", model.ParentKey));
        element.Add(new XAttribute("fileoffset", $"{model.FileOffset:X}"));
        element.Add(new XAttribute("compression", model.Compression));
        return element;
    }

    private static XElement Serialize(TilemapModel model)
    {
        var element = new XElement("tilemap");
        element.Add(new XAttribute("mapdatafile", model.MapDataKey));
        element.Add(new XAttribute("mapoffset", $"{model.MapOffset:X}"));
        element.Add(new XAttribute("width", model.Width));
        element.Add(new XAttribute("height", model.Height));

        if (model.Layout != MapScreenLayout.Linear)
            element.Add(new XAttribute("layout", model.Layout.ToXmlName()));

        element.Add(new XAttribute("format", model.FormatName));
        element.Add(new XAttribute("tiledatafile", model.TileDataKey));
        element.Add(new XAttribute("tileoffset", $"{model.TileOffset:X}"));
        element.Add(new XAttribute("codec", model.CodecName));

        if (model.FirstTile != 0)
            element.Add(new XAttribute("firsttile", model.FirstTile));

        foreach (var (index, paletteKey) in model.PaletteSlots.OrderBy(x => x.Key))
            element.Add(new XElement("paletteslot", new XAttribute("index", index), new XAttribute("palette", paletteKey)));

        return element;
    }

    private XElement Serialize(PaletteModel paletteModel)
    {
        var element = new XElement("palette");
        element.Add(new XAttribute("datafile", paletteModel.DataFileKey!));
        element.Add(new XAttribute("color", paletteModel.ColorModel.ToString()));
        element.Add(new XAttribute("zeroindextransparent", paletteModel.ZeroIndexTransparent));

        foreach (var source in paletteModel.ColorSources)
        {
            if (source is FileColorSourceModel fileSource)
            {
                var fileElement = new XElement("filesource");
                fileElement.Add(new XAttribute("fileoffset", $"{fileSource.FileAddress:X}"));

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
            else if (source is ScatteredColorSourceModel scatteredSource)
            {
                var scatteredElement = new XElement("scatteredcolor");
                scatteredElement.Add(new XAttribute("entries", scatteredSource.Entries));

                foreach (var (field, stride) in scatteredSource.Fields)
                {
                    var fieldElement = new XElement("field");
                    fieldElement.Add(new XAttribute("fileoffset", $"{field.Offset:X}"));

                    if (field.LowBit != 0)
                        fieldElement.Add(new XAttribute("lowbit", field.LowBit));

                    fieldElement.Add(new XAttribute("bits", field.Bits));

                    if (field.Shift != 0)
                        fieldElement.Add(new XAttribute("shift", field.Shift));

                    if (stride != 8 && scatteredSource.Entries > 1)
                        fieldElement.Add(new XAttribute("stride", stride));

                    scatteredElement.Add(fieldElement);
                }

                element.Add(scatteredElement);
            }
            else
            {
                throw new NotSupportedException($"Color source model of type '{source.GetType()}' is not supported");
            }
        }

        return element;
    }

    private XElement Serialize(ScatteredArrangerModel arrangerModel)
    {
        var mostUsedCodecName = arrangerModel.FindMostFrequentElementPropertyValue(x => x.CodecName);
        var mostUsedFileKey = arrangerModel.FindMostFrequentElementPropertyValue(x => x.DataFileKey);
        var mostUsedPaletteKey = arrangerModel.FindMostFrequentElementPropertyValue(x => x.PaletteKey);

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
                elNode.Add(new XAttribute("fileoffset", $"{el.FileAddress.ByteOffset:X}"));
                elNode.Add(new XAttribute("posx", el.PositionX));
                elNode.Add(new XAttribute("posy", el.PositionY));

                if (el.FileAddress.BitOffset != 0)
                    elNode.Add(new XAttribute("bitoffset", el.FileAddress.BitOffset));

                if (el.CodecName != mostUsedCodecName)
                    elNode.Add(new XAttribute("codec", el.CodecName));

                if (el.DataFileKey != mostUsedFileKey)
                    elNode.Add(new XAttribute("datafile", el.DataFileKey));

                if (el.PaletteKey != mostUsedPaletteKey && arrangerModel.ColorType == PixelColorType.Indexed)
                    elNode.Add(new XAttribute("palette", el.PaletteKey!));

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

        using var writer = new Utf8StringWriter();
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
