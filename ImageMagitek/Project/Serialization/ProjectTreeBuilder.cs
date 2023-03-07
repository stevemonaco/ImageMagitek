using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

        if (!File.Exists(fileSource.FileLocation))
            return new MagitekResult.Failed($"DataFile '{fileSource.Name}' does not exist at location '{fileSource.FileLocation}'");

        return AttachNode(dfNode, parentNodePath);
    }

    public MagitekResult AddPalette(PaletteModel paletteModel, string parentNodePath, string fileLocation)
    {
        if (paletteModel.DataFileKey is not string dataFileKey)
            return new MagitekResult.Failed($"Palette '{paletteModel.Name}' has a missing or null DataFile key");

        if (Tree?.TryGetItem<DataSource>(dataFileKey, out var df) is not true)
            return new MagitekResult.Failed($"Palette '{paletteModel.Name}' could not locate DataFile with key '{dataFileKey}'");

        var pal = paletteModel.MapToResource(_colorFactory, df);

        var palNode = new PaletteNode(pal.Name, pal)
        {
            DiskLocation = fileLocation,
            Model = paletteModel
        };

        return AttachNode(palNode, parentNodePath);
    }

    public MagitekResult AddScatteredArranger(ScatteredArrangerModel arrangerModel, string parentNodePath, string fileLocation)
    {
        var arranger = new ScatteredArranger(arrangerModel.Name, arrangerModel.ColorType, arrangerModel.Layout,
            arrangerModel.ArrangerElementSize.Width, arrangerModel.ArrangerElementSize.Height, arrangerModel.ElementPixelSize.Width, arrangerModel.ElementPixelSize.Height);

        for (int x = 0; x < arrangerModel.ElementGrid.GetLength(0); x++)
        {
            for (int y = 0; y < arrangerModel.ElementGrid.GetLength(1); y++)
            {
                var result = CreateElement(arrangerModel, x, y);

                if (result.HasSucceeded)
                {
                    arranger.SetElement(result.AsSuccess.Result, x, y);
                }
                else if (result.HasFailed)
                {
                    return new MagitekResult.Failed(result.AsError.Reason);
                }
            }
        }

        var arrangerNode = new ArrangerNode(arranger.Name, arranger)
        {
            DiskLocation = fileLocation,
            Model = arrangerModel
        };

        return AttachNode(arrangerNode, parentNodePath);
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

    /// <summary>
    /// Resolves a palette resource using the supplied project tree and falls back to a default palette if available
    /// </summary>
    /// <param name="paletteKey"></param>
    /// <returns></returns>
    private Palette ResolvePalette(string? paletteKey)
    {
        Guard.IsNotNull(Tree);

        if (string.IsNullOrEmpty(paletteKey)) // No key -> Use default palette
        {
            return _globalDefaultPalette;
        }
        else if (Tree.TryGetItem<Palette>(paletteKey, out var pal)) // Has key -> Find Palette in tree by key
        {
            return pal;
        }
        else // Key not found -> fallback to searching global palettes
        {
            var name = paletteKey.Split(Tree.PathSeparators[0]).Last();
            return _globalResources.OfType<Palette>().FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)) ?? _globalDefaultPalette;
        }
    }

    private MagitekResult<ArrangerElement?> CreateElement(ScatteredArrangerModel arrangerModel, int x, int y)
    {
        Guard.IsNotNull(Tree);

        var elementModel = arrangerModel.ElementGrid[x, y];
        IGraphicsCodec? codec = default;
        Palette? palette = default;
        DataSource? df = default;
        var address = BitAddress.Zero;

        if (elementModel is null)
        {
            return new MagitekResult<ArrangerElement?>.Success(null);
        }
        else if (arrangerModel.ColorType == PixelColorType.Indexed)
        {
            address = elementModel.FileAddress;
            var paletteKey = elementModel.PaletteKey;
            palette = ResolvePalette(paletteKey);

            if (palette is null)
            {
                return new MagitekResult<ArrangerElement?>.Failed($"Could not resolve palette '{paletteKey}' referenced by arranger '{arrangerModel.Name}'");
            }

            codec = _codecFactory.CreateCodec(elementModel.CodecName, new Size(arrangerModel.ElementPixelSize.Width, arrangerModel.ElementPixelSize.Height));
        }
        else if (arrangerModel.ColorType == PixelColorType.Direct)
        {
            address = elementModel.FileAddress;
            codec = _codecFactory.CreateCodec(elementModel.CodecName, new Size(arrangerModel.ElementPixelSize.Width, arrangerModel.ElementPixelSize.Height));
        }
        else
        {
            return new MagitekResult<ArrangerElement?>.Failed($"{nameof(CreateElement)}: Arranger '{arrangerModel.Name}' has invalid {nameof(PixelColorType)} '{arrangerModel.ColorType}'");
        }

        if (codec is null)
            return new MagitekResult<ArrangerElement?>.Failed($"{nameof(CreateElement)}: Could not create codec '{elementModel.CodecName}'");

        if (string.IsNullOrWhiteSpace(elementModel.DataFileKey))
            return new MagitekResult<ArrangerElement?>.Failed($"{nameof(CreateElement)}: {nameof(ArrangerElementModel.DataFileKey)} is empty or missing");

        if (Tree.TryGetItem<DataSource>(elementModel.DataFileKey, out df))
        {
            var pixelX = x * arrangerModel.ElementPixelSize.Width;
            var pixelY = y * arrangerModel.ElementPixelSize.Height;
            var el = new ArrangerElement(pixelX, pixelY, df, address, codec, palette, elementModel.Mirror, elementModel.Rotation);
            return new MagitekResult<ArrangerElement?>.Success(el);
        }
        else
        {
            return new MagitekResult<ArrangerElement?>.Failed($"{nameof(CreateElement)}: '{nameof(elementModel.DataFileKey)}' could not be found in the {nameof(ProjectTree)}");
        }
    }
}
