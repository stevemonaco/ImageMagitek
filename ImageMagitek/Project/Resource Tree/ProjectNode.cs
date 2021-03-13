using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project
{
    public class ProjectNode : ResourceNode
    {
        public ImageProjectModel Model { get; set; }
        public string BaseDirectory { get; set; }

        public ProjectNode(string nodeName, ImageProject resource) : base(nodeName, resource)
        {
        }
    }
}
