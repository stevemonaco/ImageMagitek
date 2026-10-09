using System;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Project;

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

    ProjectTree GetContainingProject(ResourceNode node);
    ProjectTree GetContainingProject(IProjectResource resource);
    ProjectTree? FindContainingProject(IProjectResource resource);
    bool AreResourcesInSameProject(IProjectResource a, IProjectResource b);
}
