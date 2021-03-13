using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project
{
    public class DataFileNode : ResourceNode
    {
        public DataFileModel Model { get; set; }

        public DataFileNode(string nodeName, DataFile resource) : base(nodeName, resource)
        {
        }
    }
}
