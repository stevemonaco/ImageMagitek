using System;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Project;

namespace ImageMagitek.Services;

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

    MagitekResult<ProjectTree> CreateNewProject(string projectName);
    Task<MagitekResult<ProjectTree>> CreateNewProjectWithExistingFileAsync(string projectFileName, string fileName);

    Task<MagitekResults<ProjectTree>> OpenProjectFileAsync(string projectFileName);
    Task<MagitekResult> SaveProjectAsync(ProjectTree projectTree);
    Task<MagitekResult> SaveProjectAsAsync(ProjectTree projectTree, string projectFileName);
    void CloseProject(ProjectTree projectTree);
    void CloseProjects();

    MagitekResult<ResourceNode> AddResource(ResourceNode parentNode, IProjectResource resource);
    MagitekResult<ResourceNode> CreateNewFolder(ResourceNode parentNode, string name);

    Task<MagitekResult> SaveResourceAsync(ProjectTree projectTree, ResourceNode resourceNode, bool alwaysOverwrite);
    MagitekResult CanMoveNode(ResourceNode node, ResourceNode parentNode);
    Task<MagitekResult> MoveNodeAsync(ResourceNode node, ResourceNode parentNode);

    ResourceDeletionPlan PreviewResourceDeletion(ResourceNode deleteNode);
    MagitekResult ApplyResourceDeletion(ResourceDeletionPlan plan, Palette defaultPalette);
    Task<MagitekResult> RenameResourceAsync(ResourceNode node, string newName);

    ProjectTree GetContainingProject(ResourceNode node);
    ProjectTree GetContainingProject(IProjectResource resource);
    ProjectTree? FindContainingProject(IProjectResource resource);
    bool AreResourcesInSameProject(IProjectResource a, IProjectResource b);
}
