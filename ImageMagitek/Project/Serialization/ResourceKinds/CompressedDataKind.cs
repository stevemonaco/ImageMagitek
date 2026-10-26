using System;
using System.Collections.Generic;
using System.Xml.Linq;
using ImageMagitek.Colors;

namespace ImageMagitek.Project.Serialization;

internal sealed class CompressedDataKind : ResourceKind<CompressedDataSource, CompressedDataModel, CompressedDataNode>
{
    public override string ElementName => "compresseddata";
    public override Version MinimumVersion => ProjectFormat.Current;

    // A block whose parent is another compressed block is built after it
    internal override bool BuildsInPasses => true;

    public override ResourceNode CreateNode(IProjectResource resource) => new CompressedDataNode(resource.Name, (CompressedDataSource)resource);

    internal override ResourceModel? MapToModel(ResourceNode node, Dictionary<IProjectResource, string> resourceMap, IColorFactory colorFactory) =>
        node.Item is CompressedDataSource compressed ? compressed.MapToModel(resourceMap) : null;

    internal override ResourceModel Read(XElement root, ResourceReadContext context) =>
        new CompressedDataModel
        {
            Name = context.Name,
            ParentKey = root.Attribute("parent")!.Value,
            FileOffset = long.Parse(root.Attribute("fileoffset")!.Value, System.Globalization.NumberStyles.HexNumber),
            Compression = root.Attribute("compression")!.Value
        };

    internal override XElement Write(ResourceModel model, string resourceBaseDirectory, IColorFactory colorFactory)
    {
        var compressedModel = (CompressedDataModel)model;
        var element = new XElement(ElementName);
        element.Add(new XAttribute("parent", compressedModel.ParentKey));
        element.Add(new XAttribute("fileoffset", $"{compressedModel.FileOffset:X}"));
        element.Add(new XAttribute("compression", compressedModel.Compression));
        return element;
    }

    internal override MagitekResults Build(ProjectTreeBuilder builder, ResourceModel model, string parentNodePath, string fileLocation) =>
        MagitekResults.From(builder.AddCompressedData((CompressedDataModel)model, parentNodePath, fileLocation));

    public override ResourceModel WithKeys(ResourceModel model, string name, Func<string, string> map)
    {
        var x = (CompressedDataModel)model;
        return x with { Name = name, ParentKey = map(x.ParentKey) };
    }

    internal override string Summarize(ResourceModel model) => ((CompressedDataModel)model).Compression;

    internal override IEnumerable<long> Offsets(ResourceModel model) => [((CompressedDataModel)model).FileOffset];
}
