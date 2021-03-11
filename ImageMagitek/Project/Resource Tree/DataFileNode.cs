using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project
{
    public class DataFileNode : ResourceNode
    {
        public DataFileModel Model { get; set; }

        public DataFileNode(string nodeName, IProjectResource resource) : base(nodeName, resource)
        {
        }
    }
}
