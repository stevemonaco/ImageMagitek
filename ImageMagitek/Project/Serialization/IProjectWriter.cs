namespace ImageMagitek.Project.Serialization
{
    public interface IProjectWriter
    {
        string Version { get; }

        MagitekResult WriteProject(string fileName);
        string SerializeResource(ResourceNode resourceNode);
        MagitekResult WriteResource(ResourceNode resourceNode, bool alwaysOverwrite);
    }
}
