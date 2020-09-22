using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorReader
    {
        string DescriptorVersion { get; }
        MagitekResults<ProjectTree> ReadProject(string projectFileName);
    }
}
