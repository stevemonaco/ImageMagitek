using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommunityToolkit.Diagnostics;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.Project.Serialization;

/// <summary>
/// Builds a ProjectTree from Serialization Models and handles resolving of resources
/// </summary>
internal sealed class ProjectTreeBuilder
{
    public ProjectTree? Tree { get; private set; }

    private readonly List<IProjectResource> _globalResources;
    private readonly Palette _globalDefaultPalette;
    private readonly ICodecFactory _codecFactory;
    private readonly IColorFactory _colorFactory;

    public ProjectTreeBuilder(ICodecFactory codecFactory, IColorFactory colorFactory, Palette defaultPalette, IEnumerable<IProjectResource> globalResources)
    {
        _codecFactory = codecFactory;
        _colorFactory = colorFactory;
        _globalDefaultPalette = defaultPalette;
        _globalResources = globalResources.ToList();

        if (!_globalResources.Contains(defaultPalette))
            _globalResources.Add(defaultPalette);
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

    private Palette? ResolvePalette(string? paletteKey)
    {
        Guard.IsNotNull(Tree);

        if (string.IsNullOrEmpty(paletteKey))
            return _globalDefaultPalette;

        if (Tree.TryGetItem<Palette>(paletteKey, out var pal))
            return pal;

        return _globalResources.OfType<Palette>().FirstOrDefault(x => string.Equals(x.Name, paletteKey, StringComparison.OrdinalIgnoreCase));
    }

    private IGraphicsCodec? CreateCodecOrNull(string codecName, Size elementSize)
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
