using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project;

public sealed class CompressedDataNode : ResourceNode<CompressedDataModel>
{
    public CompressedDataNode(string nodeName, CompressedDataSource resource) : base(nodeName, resource)
    {
    }
}
