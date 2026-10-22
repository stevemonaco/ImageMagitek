using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Tables;

namespace ImageMagitek.Services;

/// <summary>
/// Opens, saves and edits projects; disk-writing operations run one at a time
/// </summary>
public interface IProjectService
{
    event EventHandler<ProjectTree>? ProjectOpened;
    event EventHandler<ProjectTree>? ProjectClosed;

    /// <summary>
    /// Raised for a <see cref="ProjectTree.Changed"/> in any open project; the sender is the project's tree
    /// </summary>
    event EventHandler<ProjectTreeChange>? TreeChanged;

    /// <summary>
    /// Raised for a <see cref="ProjectTree.ResourceChanged"/> in any open project; the sender is the project's tree
    /// </summary>
    event EventHandler<IProjectResource>? ResourceChanged;

    Task<MagitekResult<ProjectTree>> CreateNewProjectAsync(string projectFileName);
    Task<MagitekResult<ProjectTree>> CreateNewProjectWithExistingFileAsync(string projectFileName, string fileName);

    Task<MagitekResults<ProjectTree>> OpenProjectFileAsync(string projectFileName);

    /// <summary>
    /// Opens a data file as a standalone tree without a project, or returns the tree that already has it open
    /// </summary>
    MagitekResult<ProjectTree> OpenDataFile(string fileName);
    Task<MagitekResult> SaveProjectAsync(ProjectTree projectTree);
    Task<MagitekResult> SaveProjectAsAsync(ProjectTree projectTree, string projectFileName);
    void CloseProject(ProjectTree projectTree);
    void CloseProjects();

    Task<MagitekResult<ResourceNode>> AddResourceAsync(ResourceNode parentNode, IProjectResource resource);
    Task<MagitekResult<ResourceNode>> CreateNewFolderAsync(ResourceNode parentNode, string name);

    /// <summary>
    /// Returns the failure that adding a resource or creating a folder with this name would return, without changing anything
    /// </summary>
    MagitekResult CanAddResource(ResourceNode parentNode, string name, bool isFolder);

    /// <summary>
    /// Returns the failure that renaming the node to this name would return, without changing anything
    /// </summary>
    MagitekResult CanRenameResource(ResourceNode node, string newName);

    Task<MagitekResult> SaveResourceAsync(ProjectTree projectTree, ResourceNode resourceNode, bool alwaysOverwrite);
    MagitekResult CanMoveNode(ResourceNode node, ResourceNode parentNode);
    Task<MagitekResult> MoveNodeAsync(ResourceNode node, ResourceNode parentNode);

    /// <summary>
    /// Copies <paramref name="sourceFileName"/> to the expected location of a missing data file and refreshes its dependents
    /// </summary>
    Task<MagitekResult> RelinkDataFileAsync(FileDataSource dataSource, string sourceFileName);

    MagitekResult<ResourceDeletionPlan> PreviewResourceDeletion(ResourceNode deleteNode);
    Task<MagitekResult> ApplyResourceDeletionAsync(ResourceDeletionPlan plan, Palette defaultPalette);
    Task<MagitekResult> RenameResourceAsync(ResourceNode node, string newName);

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
    /// a regeneration writes only its added and changed resources and deletes its removed ones
    /// </summary>
    /// <param name="saveProject">Also writes every other changed resource, as a project save does; otherwise only the generated resources and the manifest are written</param>
    /// <param name="confirmRemovals">Allows a regeneration to delete the resources whose records no longer exist</param>
    Task<MagitekResult> GenerateResourcesAsync(ProjectTree tree, GenerationPlan plan, bool saveProject = false, bool confirmRemovals = false);

    ProjectTree GetContainingProject(ResourceNode node);
    ProjectTree GetContainingProject(IProjectResource resource);
    ProjectTree? FindContainingProject(IProjectResource resource);
    bool AreResourcesInSameProject(IProjectResource a, IProjectResource b);
}
