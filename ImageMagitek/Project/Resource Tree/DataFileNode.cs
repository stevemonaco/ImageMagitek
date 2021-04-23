using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project
{
    public class DataFileNode : ResourceNode<DataFileModel>
    {
        public DataFileNode(string nodeName, DataFile resource) : base(nodeName, resource)
        {
        }
    }
}
