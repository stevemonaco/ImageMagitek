using Monaco.PathTree;

namespace ImageMagitek.Project.Serialization
{
    public interface IGameDescriptorWriter
    {
        string DescriptorVersion { get; }
        MagitekResult WriteProject(ProjectTree tree, string fileName);
    }
}
