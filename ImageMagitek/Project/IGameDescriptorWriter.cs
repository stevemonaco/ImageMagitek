using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorWriter
    {
        string DescriptorVersion { get; }
        bool WriteProject(PathTree<ProjectResourceBase> tree, string fileName);
    }
}
