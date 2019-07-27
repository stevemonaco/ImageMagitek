using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorReader
    {
        string DescriptorVersion { get; }
        PathTree<IProjectResource> ReadProject(string fileName, string baseDirectory);
    }
}
