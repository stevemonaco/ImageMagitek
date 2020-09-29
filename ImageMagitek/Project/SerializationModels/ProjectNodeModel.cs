using System.Collections.Generic;

namespace ImageMagitek.Project.Serialization
{
    public abstract class ProjectNodeModel
    {
        public string Name { get; set; }
        public ProjectNodeModel Parent { get; set; }
        internal Dictionary<string, ProjectNodeModel> ChildResources = new Dictionary<string, ProjectNodeModel>();
    }
}
