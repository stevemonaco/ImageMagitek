using CommunityToolkit.Diagnostics;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Compression;
using ImageMagitek.Tilemaps;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Schema;

namespace ImageMagitek.Project.Serialization;

public sealed class XmlProjectSerializerFactory : IProjectSerializerFactory
{
    public List<IProjectResource> GlobalResources { get; }

    private readonly string _resourceSchemaFileName;
    private readonly ICodecFactory _codecFactory;
    private readonly IColorFactory _colorFactory;
    private readonly ICompressorFactory _compressorFactory;
    private readonly IMapFormatFactory _mapFormatFactory;

    public XmlProjectSerializerFactory(string resourceSchemaFileName,
        ICodecFactory codecFactory, IColorFactory colorFactory, IEnumerable<IProjectResource> globalResources,
        ICompressorFactory? compressorFactory = null, IMapFormatFactory? mapFormatFactory = null)
    {
        _resourceSchemaFileName = resourceSchemaFileName;
        _codecFactory = codecFactory;
        _colorFactory = colorFactory;
        _compressorFactory = compressorFactory ?? new CompressorFactory();
        _mapFormatFactory = mapFormatFactory ?? new MapFormatFactory();
        GlobalResources = globalResources.ToList();
    }

    public IProjectReader CreateReader()
    {
        var resourceSchemas = CreateSchemas(_resourceSchemaFileName);

        return new XmlProjectReader(resourceSchemas, _codecFactory, _colorFactory, GlobalResources, _compressorFactory, _mapFormatFactory);
    }

    public IProjectWriter CreateWriter(ProjectTree tree)
    {
        return new XmlProjectWriter(tree, _colorFactory, GlobalResources);
    }

    public IMapFormatFactory MapFormatFactory => _mapFormatFactory;

    /// <inheritdoc/>
    public MagitekResults BuildResources(ProjectTree tree, IReadOnlyList<(ResourceModel Model, string ParentNodePath, string FileLocation)> models)
    {
        var builder = new ProjectTreeBuilder(tree, _codecFactory, _colorFactory, GlobalResources.OfType<Palette>().First(), GlobalResources,
            _compressorFactory, _mapFormatFactory);
        var reasons = new List<string>();

        foreach (var (model, parentNodePath, fileLocation) in models)
        {
            reasons.AddRange(model switch
            {
                CompressedDataModel compressed => ReasonsOf(builder.AddCompressedData(compressed, parentNodePath, fileLocation)),
                PaletteModel palette => ReasonsOf(builder.AddPalette(palette, parentNodePath, fileLocation)),
                ScatteredArrangerModel arranger => ReasonsOf(builder.AddScatteredArranger(arranger, parentNodePath, fileLocation)),
                MetatileSetModel set => ReasonsOf(builder.AddMetatileSet(set, parentNodePath, fileLocation)),
                TilemapModel tilemap => ReasonsOf(builder.AddTilemap(tilemap, parentNodePath, fileLocation)),
                MetatileMapModel map => ReasonsOf(builder.AddMetatileMap(map, parentNodePath, fileLocation)),
                _ => [$"Cannot build a resource from a model of type '{model.GetType().Name}'"]
            });
        }

        return reasons.Count > 0 ? new MagitekResults.Failed(reasons) : MagitekResults.SuccessResults;
    }

    private static IEnumerable<string> ReasonsOf(MagitekResult result) => result.HasFailed ? [result.AsError.Reason] : [];
    private static IEnumerable<string> ReasonsOf(MagitekResults result) => result.HasFailed ? result.AsError.Reasons : [];

    private XmlSchemaSet CreateSchemas(string resourceSchemaFileName)
    {
        var resourceSchemaText = File.ReadAllText(resourceSchemaFileName);

        var resourceSchema = XmlSchema.Read(new StringReader(resourceSchemaText), null);
        Guard.IsNotNull(resourceSchema);

        var resourceSchemaSet = new XmlSchemaSet();
        resourceSchemaSet.Add(resourceSchema);

        return resourceSchemaSet;
    }
}
