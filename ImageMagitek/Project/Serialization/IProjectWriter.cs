using System.Collections.Generic;
using System.Threading.Tasks;

namespace ImageMagitek.Project.Serialization;

public interface IProjectWriter
{
    /// <summary>
    /// Writes every changed resource in one transaction, together with <paramref name="additionalFiles"/>
    /// </summary>
    /// <param name="additionalFiles">Files outside the tree, such as a table manifest, committed in the same transaction</param>
    Task<MagitekResult> WriteProjectAsync(string fileName, IReadOnlyList<(string Path, string Contents)>? additionalFiles = null);
    string SerializeResource(ResourceNode resourceNode);
    Task<MagitekResult> WriteResourceAsync(ResourceNode resourceNode, bool alwaysOverwrite);

    /// <summary>
    /// Writes each model to its node's disk location in one transaction, setting the nodes' persisted models only on success
    /// </summary>
    Task<MagitekResult> WriteModelsAsync(IReadOnlyList<(ResourceNode Node, ResourceModel Model)> writes);
}
