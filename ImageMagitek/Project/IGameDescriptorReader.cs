using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorReader
    {
        string DescriptorVersion { get; }
        IPathTree<IProjectResource> ReadProject(string fileName, string baseDirectory);
    }
}
