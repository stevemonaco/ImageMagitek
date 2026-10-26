using System.Collections.Generic;
using System.Threading.Tasks;
using ImageMagitek.Project;
using ImageMagitek.Tables;

namespace ImageMagitek.Services;

/// <summary>
/// Plans and generates resources in bulk into open projects; generation runs one at a time with the project service's writes
/// </summary>
public interface IResourceGenerationService
{
    /// <summary>
    /// Plans generating resources from a table definition over a data file into a folder, writing nothing
    /// </summary>
    /// <param name="targetFolderKey">Key of the folder to generate into; missing folders are created by generation</param>
    /// <param name="overwriteEdited">For a regeneration, also rewrite or remove resources edited since they were generated</param>
    MagitekResult<GenerationPlan> PlanGeneration(ProjectTree tree, string dataFileKey, TableDefinition definition, string targetFolderKey,
        bool overwriteEdited = false);

    /// <summary>
    /// Lists the existing resources the plan's keys and names clash with, the check generation refuses on, writing nothing
    /// </summary>
    IReadOnlyList<string> FindGenerationConflicts(ProjectTree tree, GenerationPlan plan);

    /// <summary>
    /// Creates the plan's folders and resources and writes them with the manifest in one transaction, rolling back on failure;
    /// a regeneration writes only its added and changed resources and the resources whose saved form changes with them, and deletes its removed ones
    /// </summary>
    /// <param name="saveProject">Also writes every other changed resource, as a project save does; otherwise only the generated resources and the manifest are written</param>
    /// <param name="confirmRemovals">Allows a regeneration to delete the resources whose records no longer exist</param>
    Task<MagitekResult> GenerateResourcesAsync(ProjectTree tree, GenerationPlan plan, bool saveProject = false, bool confirmRemovals = false);
}
