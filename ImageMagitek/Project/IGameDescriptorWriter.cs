using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorWriter
    {
        string DescriptorVersion { get; }
        MagitekResult WriteProject(ProjectTree tree, string fileName);
    }
}
