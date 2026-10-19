using CommunityToolkit.Diagnostics;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Compression;
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

    public XmlProjectSerializerFactory(string resourceSchemaFileName,
        ICodecFactory codecFactory, IColorFactory colorFactory, IEnumerable<IProjectResource> globalResources,
        ICompressorFactory? compressorFactory = null)
    {
        _resourceSchemaFileName = resourceSchemaFileName;
        _codecFactory = codecFactory;
        _colorFactory = colorFactory;
        _compressorFactory = compressorFactory ?? new CompressorFactory();
        GlobalResources = globalResources.ToList();
    }

    public IProjectReader CreateReader()
    {
        var resourceSchemas = CreateSchemas(_resourceSchemaFileName);

        return new XmlProjectReader(resourceSchemas, _codecFactory, _colorFactory, GlobalResources, _compressorFactory);
    }

    public IProjectWriter CreateWriter(ProjectTree tree)
    {
        return new XmlProjectWriter(tree, _colorFactory, GlobalResources);
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
