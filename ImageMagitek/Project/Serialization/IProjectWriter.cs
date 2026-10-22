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
    /// <summary>
    /// Writes those of <paramref name="nodes"/> that changed, leaving every other resource unwritten, together with <paramref name="additionalFiles"/> in one transaction
    /// </summary>
    Task<MagitekResult> WriteResourcesAsync(IReadOnlyList<ResourceNode> nodes, IReadOnlyList<(string Path, string Contents)>? additionalFiles = null);
    string SerializeResource(ResourceNode resourceNode);

    /// <summary>
    /// Serializes a model to the text a write would produce for it
    /// </summary>
    string SerializeModel(ResourceModel model);

    /// <summary>
    /// Maps the node's resource to the model a write would serialize, failing when it cannot be written
    /// </summary>
    /// <param name="resourceMap">The key of every resource the model may reference; null maps the tree as it is</param>
    MagitekResult<ResourceModel> MapCurrentModel(ResourceNode node, Dictionary<IProjectResource, string>? resourceMap = null);
    Task<MagitekResult> WriteResourceAsync(ResourceNode resourceNode, bool alwaysOverwrite);

    /// <summary>
    /// Writes each model to its node's disk location in one transaction, setting the nodes' persisted models only on success
    /// </summary>
    Task<MagitekResult> WriteModelsAsync(IReadOnlyList<(ResourceNode Node, ResourceModel Model)> writes);
}
