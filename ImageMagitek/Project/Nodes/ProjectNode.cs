namespace ImageMagitek.Project
{
    public class ProjectNode : ResourceNode<ImageProject>
    {
        public ImageProject Project { get; set; }
        public ProjectNode(string name, ImageProject project) : base(name, project)
        {
            Project = project;
        }
    }
}
