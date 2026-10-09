using System.Collections.Generic;
using System.Threading.Tasks;

namespace ImageMagitek.Project.Serialization;

public interface IProjectWriter
{
    Task<MagitekResult> WriteProjectAsync(string fileName);
    string SerializeResource(ResourceNode resourceNode);
    Task<MagitekResult> WriteResourceAsync(ResourceNode resourceNode, bool alwaysOverwrite);

    /// <summary>
    /// Writes each model to its node's disk location in one transaction, setting the nodes' persisted models only on success
    /// </summary>
    Task<MagitekResult> WriteModelsAsync(IReadOnlyList<(ResourceNode Node, ResourceModel Model)> writes);
}
