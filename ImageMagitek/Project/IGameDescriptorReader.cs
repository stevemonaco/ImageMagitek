using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorReader
    {
        string DescriptorVersion { get; }
        MagitekResults<IPathTree<IProjectResource>> ReadProject(string projectFileName);
    }
}
