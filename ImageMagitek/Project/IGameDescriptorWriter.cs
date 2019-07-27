using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorWriter
    {
        string DescriptorVersion { get; }
        bool WriteProject(PathTree<IProjectResource> tree, string fileName);
    }
}
