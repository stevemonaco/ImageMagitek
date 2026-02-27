using System.Collections.Generic;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Project;

namespace ImageMagitek.Services;

public interface IProjectService
{
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

    MagitekResult ApplyResourceDeletionChanges(IList<ResourceChange> changes, Palette defaultPalette);
    IEnumerable<ResourceChange> PreviewResourceDeletionChanges(ResourceNode deleteNode);
    Task<MagitekResult> RenameResourceAsync(ResourceNode node, string newName);

    ProjectTree GetContainingProject(ResourceNode node);
    ProjectTree GetContainingProject(IProjectResource resource);
    bool AreResourcesInSameProject(IProjectResource a, IProjectResource b);
}
