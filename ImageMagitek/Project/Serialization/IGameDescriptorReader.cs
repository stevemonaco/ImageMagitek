namespace ImageMagitek.Project.Serialization
{
    public interface IGameDescriptorReader
    {
        string DescriptorVersion { get; }
        MagitekResults<ProjectTree> ReadProject(string projectFileName);
    }
}
