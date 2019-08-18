using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorWriter
    {
        string DescriptorVersion { get; }
        bool WriteProject(IPathTree<IProjectResource> tree, string fileName);
    }
}
