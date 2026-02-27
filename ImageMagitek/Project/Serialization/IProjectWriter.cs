using System.Threading.Tasks;

namespace ImageMagitek.Project.Serialization;

public interface IProjectWriter
{
    string Version { get; }

    Task<MagitekResult> WriteProjectAsync(string fileName);
    string SerializeResource(ResourceNode resourceNode);
    Task<MagitekResult> WriteResourceAsync(ResourceNode resourceNode, bool alwaysOverwrite);
}
