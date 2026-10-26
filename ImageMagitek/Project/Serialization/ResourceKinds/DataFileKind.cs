using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using ImageMagitek.Colors;

namespace ImageMagitek.Project.Serialization;

internal sealed class DataFileKind : ResourceKind<DataSource, DataFileModel, DataFileNode>
{
    public override string ElementName => "datafile";

    public override ResourceNode CreateNode(IProjectResource resource) => new DataFileNode(resource.Name, (DataSource)resource);

    internal override ResourceModel? MapToModel(ResourceNode node, Dictionary<IProjectResource, string> resourceMap, IColorFactory colorFactory) =>
        node.Item is FileDataSource df ? df.MapToModel() : null;

    internal override ResourceModel Read(XElement root, ResourceReadContext context) =>
        new DataFileModel
        {
            Name = context.Name,
            Location = Path.Combine(context.BaseDirectory, root.Attribute("location")!.Value)
        };

    internal override XElement Write(ResourceModel model, string resourceBaseDirectory, IColorFactory colorFactory)
    {
        var dataFileModel = (DataFileModel)model;
        var element = new XElement(ElementName);

        var relativePath = Path.GetRelativePath(resourceBaseDirectory, dataFileModel.Location);
        element.Add(new XAttribute("location", relativePath));
        return element;
    }

    internal override MagitekResults Build(ProjectTreeBuilder builder, ResourceModel model, string parentNodePath, string fileLocation) =>
        MagitekResults.From(builder.AddDataFile((DataFileModel)model, parentNodePath, fileLocation));
}
