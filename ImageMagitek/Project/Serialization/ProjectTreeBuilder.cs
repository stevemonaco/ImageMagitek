using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommunityToolkit.Diagnostics;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Compression;
using ImageMagitek.Metatiles;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.Project.Serialization;

/// <summary>
/// Builds a ProjectTree from Serialization Models and handles resolving of resources
/// </summary>
internal sealed class ProjectTreeBuilder
{
    public ProjectTree? Tree { get; private set; }

    private static readonly Dictionary<string, string> _renamedGlobalPalettes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DefaultRgba32"] = "Classic"
    };

    private readonly List<IProjectResource> _globalResources;
    private readonly Palette _globalDefaultPalette;
    private readonly ICodecFactory _codecFactory;
    private readonly IColorFactory _colorFactory;
    private readonly ICompressorFactory _compressorFactory;
    private readonly IMapFormatFactory _mapFormatFactory;

    public ProjectTreeBuilder(ICodecFactory codecFactory, IColorFactory colorFactory, Palette defaultPalette, IEnumerable<IProjectResource> globalResources,
        ICompressorFactory compressorFactory, IMapFormatFactory mapFormatFactory)
    {
        _mapFormatFactory = mapFormatFactory;
        _codecFactory = codecFactory;
        _colorFactory = colorFactory;
        _compressorFactory = compressorFactory;
        _globalDefaultPalette = defaultPalette;
        _globalResources = globalResources.ToList();

        if (!_globalResources.Contains(defaultPalette))
            _globalResources.Add(defaultPalette);
    }

    /// <summary>
    /// Builds into an existing tree, resolving keys against the resources it already holds
    /// </summary>
    public ProjectTreeBuilder(ProjectTree tree, ICodecFactory codecFactory, IColorFactory colorFactory, Palette defaultPalette,
        IEnumerable<IProjectResource> globalResources, ICompressorFactory compressorFactory, IMapFormatFactory mapFormatFactory)
        : this(codecFactory, colorFactory, defaultPalette, globalResources, compressorFactory, mapFormatFactory)
    {
        Tree = tree;
    }

    public MagitekResult AddProject(ImageProjectModel projectModel, string baseDirectory, string projectFileName)
    {
        if (Tree?.Root is not null)
            return new MagitekResult.Failed($"Attempted to add a new project '{projectModel?.Name}' to an existing project");

        var root = new ProjectNode(baseDirectory, projectModel.Name, projectModel.MapToResource())
        {
            DiskLocation = projectFileName,
            Model = projectModel
        };

        Tree = new ProjectTree(root);

        return MagitekResult.SuccessResult;
    }

    public MagitekResult AddFolder(ResourceFolderModel folderModel, string parentNodePath, string diskLocation)
    {
        var folder = new ResourceFolder(folderModel.Name);

        var folderNode = new ResourceFolderNode(folder.Name, folder)
        {
            DiskLocation = diskLocation
        };

        return AttachNode(folderNode, parentNodePath);
    }

    public MagitekResult AddDataFile(DataFileModel dfModel, string parentNodePath, string fileLocation)
    {
        var fileSource = new FileDataSource(dfModel.Name, dfModel.Location);

        var dfNode = new DataFileNode(fileSource.Name, fileSource)
        {
            DiskLocation = fileLocation,
            Model = dfModel
        };

        return AttachNode(dfNode, parentNodePath);
    }

    /// <summary>
    /// Attaches compressed data over its parent data source, which must already be in the tree. A compressor that is not registered
    /// is not a failure here; the source reports it through <see cref="CompressedDataSource.DecompressionError"/>.
    /// </summary>
    public MagitekResult AddCompressedData(CompressedDataModel model, string parentNodePath, string fileLocation)
    {
        var pathKey = CreatePathKey(parentNodePath, model.Name);

        if (Tree?.TryGetItem<DataSource>(model.ParentKey, out var parent) is not true)
            return new MagitekResult.Failed($"Compressed data '{pathKey}' references data file '{model.ParentKey}', which is not in the project");

        var source = new CompressedDataSource(model.Name, parent, model.FileOffset, model.Compression, _compressorFactory);
        var node = new CompressedDataNode(source.Name, source)
        {
            DiskLocation = fileLocation,
            Model = model
        };

        return AttachNode(node, parentNodePath);
    }

    public MagitekResult AddPalette(PaletteModel paletteModel, string parentNodePath, string fileLocation)
    {
        var pathKey = CreatePathKey(parentNodePath, paletteModel.Name);

        if (paletteModel.DataFileKey is not string dataFileKey)
            return new MagitekResult.Failed($"Palette '{pathKey}' has no data file");

        if (Tree?.TryGetItem<DataSource>(dataFileKey, out var df) is not true)
            return new MagitekResult.Failed($"Palette '{pathKey}' references data file '{dataFileKey}', which is not in the project");

        var pal = paletteModel.MapToResource(_colorFactory, df);

        var palNode = new PaletteNode(pal.Name, pal)
        {
            DiskLocation = fileLocation,
            Model = paletteModel
        };

        return AttachNode(palNode, parentNodePath);
    }

    /// <summary>
    /// Builds and attaches a tilemap over data sources and palettes that must already be in the tree, collecting one failure reason per unresolved
    /// key, format or codec
    /// </summary>
    public MagitekResults AddTilemap(TilemapModel model, string parentNodePath, string fileLocation)
    {
        Guard.IsNotNull(Tree);
        var pathKey = CreatePathKey(parentNodePath, model.Name);
        var reasons = new List<string>();

        if (!Tree.TryGetItem<DataSource>(model.MapDataKey, out var mapSource))
            reasons.Add($"Tilemap '{pathKey}' references map data file '{model.MapDataKey}', which is not in the project");

        if (!Tree.TryGetItem<DataSource>(model.TileDataKey, out var tileSource))
            reasons.Add($"Tilemap '{pathKey}' references tile data file '{model.TileDataKey}', which is not in the project");

        var format = _mapFormatFactory.Find(model.FormatName);
        if (format is null)
            reasons.Add($"Tilemap '{pathKey}' uses unknown map format '{model.FormatName}'");

        var slots = ResolveTileInputs($"Tilemap '{pathKey}'", model.CodecName, format, model.PaletteSlots, reasons);

        ScatteredArranger? tilePalette = null;
        if (model.TilePaletteKey is { } tilePaletteKey)
        {
            if (Tree.TryGetNode(tilePaletteKey, out var tilePaletteNode) && tilePaletteNode.Item is ScatteredArranger arranger)
                tilePalette = arranger;
            else
                reasons.Add($"Tilemap '{pathKey}' references tile palette arranger '{tilePaletteKey}', which is not a scattered arranger in the project");
        }

        if (reasons.Count > 0)
            return new MagitekResults.Failed(reasons);

        TilemapArranger tilemap;
        try
        {
            tilemap = new TilemapArranger(model.Name, mapSource!, model.MapOffset, format!, model.Layout, model.Width, model.Height,
                tileSource!, model.TileOffset, model.CodecName, model.FirstTile, slots, _codecFactory, model.EmptyTile, model.TileCount);
        }
        catch (ArgumentException ex)
        {
            return new MagitekResults.Failed([$"Tilemap '{pathKey}': {ex.Message}"]);
        }

        tilemap.TilePaletteArranger = tilePalette;

        var node = new TilemapNode(tilemap.Name, tilemap)
        {
            DiskLocation = fileLocation,
            Model = model
        };

        var attachResult = AttachNode(node, parentNodePath);
        return attachResult.HasSucceeded ? MagitekResults.SuccessResults : new MagitekResults.Failed([attachResult.AsError.Reason]);
    }

    /// <summary>
    /// Builds and attaches a metatile set over data sources and palettes that must already be in the tree, collecting one failure reason per
    /// unresolved key, format or codec
    /// </summary>
    public MagitekResults AddMetatileSet(MetatileSetModel model, string parentNodePath, string fileLocation)
    {
        Guard.IsNotNull(Tree);
        var pathKey = CreatePathKey(parentNodePath, model.Name);
        var reasons = new List<string>();

        if (!Tree.TryGetItem<DataSource>(model.MetatileDataKey, out var metatileSource))
            reasons.Add($"Metatile set '{pathKey}' references metatile data file '{model.MetatileDataKey}', which is not in the project");

        if (!Tree.TryGetItem<DataSource>(model.TileDataKey, out var tileSource))
            reasons.Add($"Metatile set '{pathKey}' references tile data file '{model.TileDataKey}', which is not in the project");

        DataSource? attributeSource = null;
        if (model.AttributeDataKey is { } attributeKey)
        {
            if (Tree.TryGetItem<DataSource>(attributeKey, out var found))
                attributeSource = found;
            else
                reasons.Add($"Metatile set '{pathKey}' references attribute data file '{attributeKey}', which is not in the project");
        }

        if (model.AttributeDataKey is not null && model.AttributeSize is null)
            reasons.Add($"Metatile set '{pathKey}' has an attribute data file but no attribute size");

        var format = _mapFormatFactory.Find(model.FormatName);
        if (format is null)
            reasons.Add($"Metatile set '{pathKey}' uses unknown map format '{model.FormatName}'");

        var slots = ResolveTileInputs($"Metatile set '{pathKey}'", model.CodecName, format, model.PaletteSlots, reasons);

        if (reasons.Count > 0)
            return new MagitekResults.Failed(reasons);

        MetatileSet set;
        try
        {
            set = new MetatileSet(model.Name, metatileSource!, model.MetatileOffset, model.MetatileCount,
                new Size(model.MetatileWidth, model.MetatileHeight), model.Layers, format!, model.Order,
                tileSource!, model.TileOffset, model.CodecName, model.FirstTile, model.TileCount, slots, _codecFactory,
                attributeSource, model.AttributeOffset, model.AttributeSize ?? 0);
        }
        catch (ArgumentException ex)
        {
            return new MagitekResults.Failed([$"Metatile set '{pathKey}': {ex.Message}"]);
        }

        var node = new MetatileSetNode(set.Name, set)
        {
            DiskLocation = fileLocation,
            Model = model
        };

        var attachResult = AttachNode(node, parentNodePath);
        return attachResult.HasSucceeded ? MagitekResults.SuccessResults : new MagitekResults.Failed([attachResult.AsError.Reason]);
    }

    /// <summary>
    /// Builds and attaches a metatile map over a data source and metatile sets that must already be in the tree, collecting one failure reason per
    /// unresolved key or format
    /// </summary>
    public MagitekResults AddMetatileMap(MetatileMapModel model, string parentNodePath, string fileLocation)
    {
        Guard.IsNotNull(Tree);
        var pathKey = CreatePathKey(parentNodePath, model.Name);
        var reasons = new List<string>();

        if (!Tree.TryGetItem<DataSource>(model.MapDataKey, out var mapSource))
            reasons.Add($"Metatile map '{pathKey}' references map data file '{model.MapDataKey}', which is not in the project");

        var format = _mapFormatFactory.Find(model.FormatName);
        if (format is null)
            reasons.Add($"Metatile map '{pathKey}' uses unknown block format '{model.FormatName}'");

        var sets = new List<MetatileMapSet>();
        foreach (var (setKey, setBase) in model.Sets)
        {
            if (Tree.TryGetNode(setKey, out var setNode) && setNode.Item is MetatileSet set)
                sets.Add(new MetatileMapSet(set, setBase));
            else
                reasons.Add($"Metatile map '{pathKey}' references metatile set '{setKey}', which is not a metatile set in the project");
        }

        if (reasons.Count > 0)
            return new MagitekResults.Failed(reasons);

        MetatileMap map;
        try
        {
            map = new MetatileMap(model.Name, mapSource!, model.MapOffset, format!, model.Layout, model.Width, model.Height, sets,
                model.EmptyBlock);
        }
        catch (ArgumentException ex)
        {
            return new MagitekResults.Failed([$"Metatile map '{pathKey}': {ex.Message}"]);
        }

        var node = new MetatileMapNode(map.Name, map)
        {
            DiskLocation = fileLocation,
            Model = model
        };

        var attachResult = AttachNode(node, parentNodePath);
        return attachResult.HasSucceeded ? MagitekResults.SuccessResults : new MagitekResults.Failed([attachResult.AsError.Reason]);
    }

    /// <summary>
    /// Builds and attaches an arranger, collecting one failure reason per distinct unresolved key or codec
    /// </summary>
    public MagitekResults AddScatteredArranger(ScatteredArrangerModel arrangerModel, string parentNodePath, string fileLocation)
    {
        var pathKey = CreatePathKey(parentNodePath, arrangerModel.Name);
        var arranger = new ScatteredArranger(arrangerModel.Name, arrangerModel.ColorType, arrangerModel.Layout,
            arrangerModel.ArrangerElementSize.Width, arrangerModel.ArrangerElementSize.Height, arrangerModel.ElementPixelSize.Width, arrangerModel.ElementPixelSize.Height);

        var reasons = new Dictionary<string, string>();

        for (int y = 0; y < arrangerModel.ElementGrid.GetLength(1); y++)
        {
            for (int x = 0; x < arrangerModel.ElementGrid.GetLength(0); x++)
            {
                if (TryCreateElement(arrangerModel, pathKey, x, y, reasons, out var element))
                    arranger.SetElement(element, x, y);
            }
        }

        if (reasons.Count > 0)
            return new MagitekResults.Failed(reasons.Values);

        var arrangerNode = new ArrangerNode(arranger.Name, arranger)
        {
            DiskLocation = fileLocation,
            Model = arrangerModel
        };

        var attachResult = AttachNode(arrangerNode, parentNodePath);
        return attachResult.HasSucceeded ? MagitekResults.SuccessResults : new MagitekResults.Failed([attachResult.AsError.Reason]);
    }

    private MagitekResult AttachNode(ResourceNode node, string parentNodePath)
    {
        if (Tree?.TryGetNode(parentNodePath, out var parentNode) is true)
        {
            parentNode.AttachChildNode(node);
            return MagitekResult.SuccessResult;
        }
        else
            return new MagitekResult.Failed($"Could not find node with path '{parentNodePath}' to attach node '{node.Name}'");
    }

    private static string CreatePathKey(string parentNodePath, string name) =>
        string.IsNullOrEmpty(parentNodePath) ? $"/{name}" : $"/{parentNodePath}/{name}";

    private Palette?[] ResolveTileInputs(string owner, string codecName, MapFormat? format, IReadOnlyDictionary<int, string> paletteSlots, List<string> reasons)
    {
        var codec = CreateCodecOrNull(codecName, null);
        if (codec is null)
            reasons.Add($"{owner} uses unknown codec '{codecName}'");
        else if (codec is not IIndexedCodec)
            reasons.Add($"{owner} uses direct codec '{codecName}'; tile codecs must be indexed");

        var slots = new Palette?[format?.PaletteSlotCount ?? 0];
        foreach (var (index, paletteKey) in paletteSlots)
        {
            if (format is not null && index >= slots.Length)
                reasons.Add($"{owner} assigns palette slot {index}, but map format '{format.Name}' has {slots.Length} slots");
            else if (ResolvePalette(paletteKey) is not { } palette)
                reasons.Add($"{owner} references palette '{paletteKey}', which is not in the project or the global palettes");
            else if (format is not null)
                slots[index] = palette;
        }

        return slots;
    }

    private Palette? ResolvePalette(string? paletteKey)
    {
        Guard.IsNotNull(Tree);

        if (string.IsNullOrEmpty(paletteKey))
            return _globalDefaultPalette;

        if (Tree.TryGetItem<Palette>(paletteKey, out var pal))
            return pal;

        return FindGlobalPalette(paletteKey)
            ?? (_renamedGlobalPalettes.TryGetValue(paletteKey, out var newName) ? FindGlobalPalette(newName) : null);
    }

    private Palette? FindGlobalPalette(string name) =>
        _globalResources.OfType<Palette>().FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    private IGraphicsCodec? CreateCodecOrNull(string codecName, Size? elementSize)
    {
        try
        {
            return _codecFactory.CreateCodec(codecName, elementSize);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private bool TryCreateElement(ScatteredArrangerModel arrangerModel, string arrangerKey, int x, int y,
        Dictionary<string, string> reasons, out ArrangerElement? element)
    {
        Guard.IsNotNull(Tree);
        element = null;

        var elementModel = arrangerModel.ElementGrid[x, y];
        if (elementModel is null)
            return true;

        var position = $"element ({x}, {y})";
        var failed = false;

        var codec = CreateCodecOrNull(elementModel.CodecName, arrangerModel.ElementPixelSize);
        if (codec is null)
        {
            Fail($"codec:{elementModel.CodecName}", $"Arranger '{arrangerKey}' {position} uses unknown codec '{elementModel.CodecName}'");
        }
        else if (arrangerModel.ColorType == PixelColorType.Indexed)
        {
            if (codec is not IIndexedCodec indexedCodec)
            {
                Fail($"codec:{elementModel.CodecName}", $"Arranger '{arrangerKey}' {position}: indexed arranger uses direct codec '{elementModel.CodecName}'");
            }
            else if (ResolvePalette(elementModel.PaletteKey) is Palette palette)
            {
                indexedCodec.Palette = palette;
            }
            else
            {
                Fail($"palette:{elementModel.PaletteKey}", $"Arranger '{arrangerKey}' {position} references palette '{elementModel.PaletteKey}', which is not in the project or the global palettes");
            }
        }
        else if (codec is not IDirectCodec)
        {
            Fail($"codec:{elementModel.CodecName}", $"Arranger '{arrangerKey}' {position}: direct arranger uses indexed codec '{elementModel.CodecName}'");
        }

        DataSource? df = null;
        if (string.IsNullOrWhiteSpace(elementModel.DataFileKey))
        {
            Fail("datafile:", $"Arranger '{arrangerKey}' {position} has no data file");
        }
        else if (!Tree.TryGetItem<DataSource>(elementModel.DataFileKey, out df))
        {
            Fail($"datafile:{elementModel.DataFileKey}", $"Arranger '{arrangerKey}' {position} references data file '{elementModel.DataFileKey}', which is not in the project");
        }

        if (failed)
            return false;

        var pixelX = x * arrangerModel.ElementPixelSize.Width;
        var pixelY = y * arrangerModel.ElementPixelSize.Height;
        element = new ArrangerElement(pixelX, pixelY, df!, elementModel.FileAddress, codec!, elementModel.Mirror, elementModel.Rotation);
        return true;

        void Fail(string key, string reason)
        {
            failed = true;
            reasons.TryAdd(key, reason);
        }
    }
}
