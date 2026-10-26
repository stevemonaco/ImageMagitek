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
    public ResourceKindRegistry Kinds { get; }

    private readonly string _resourceSchemaFileName;
    private readonly ICodecFactory _codecFactory;
    private readonly IColorFactory _colorFactory;
    private readonly ICompressorFactory _compressorFactory;
    private readonly IMapFormatFactory _mapFormatFactory;

    public XmlProjectSerializerFactory(string resourceSchemaFileName,
        ICodecFactory codecFactory, IColorFactory colorFactory, IEnumerable<IProjectResource> globalResources,
        ICompressorFactory? compressorFactory = null, IMapFormatFactory? mapFormatFactory = null, ResourceKindRegistry? kinds = null)
    {
        Kinds = kinds ?? ResourceKindRegistry.BuiltIn;
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

        return new XmlProjectReader(resourceSchemas, _codecFactory, _colorFactory, GlobalResources, _compressorFactory, _mapFormatFactory, Kinds);
    }

    public IProjectWriter CreateWriter(ProjectTree tree)
    {
        return new XmlProjectWriter(tree, _colorFactory, GlobalResources, Kinds);
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
            if (Kinds.FindByModel(model) is not { } kind)
                reasons.Add($"Cannot build a resource from a model of type '{model.GetType().Name}'");
            else if (kind.Build(builder, model, parentNodePath, fileLocation) is { HasFailed: true } failed)
                reasons.AddRange(failed.AsError.Reasons);
        }

        return reasons.Count > 0 ? new MagitekResults.Failed(reasons) : MagitekResults.SuccessResults;
    }

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
