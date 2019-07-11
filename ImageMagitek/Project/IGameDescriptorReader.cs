using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorReader
    {
        string DescriptorVersion { get; }
        PathTree<ProjectResourceBase> ReadProject(string fileName, string baseDirectory);
    }
}
