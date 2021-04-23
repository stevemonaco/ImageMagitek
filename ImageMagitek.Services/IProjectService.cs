using System.Collections.Generic;
using ImageMagitek.Colors;
using ImageMagitek.Project;

namespace ImageMagitek.Services
{
    public interface IProjectService
    {
        ISet<ProjectTree> Projects { get; }

        MagitekResult<ProjectTree> NewProject(string projectName);
        MagitekResults<ProjectTree> OpenProjectFile(string projectFileName);
        MagitekResult SaveProject(ProjectTree projectTree);
        MagitekResult SaveProjectAs(ProjectTree projectTree, string projectFileName);
        void CloseProject(ProjectTree projectTree);
        void CloseProjects();

        MagitekResult<ResourceNode> AddResource(ResourceNode parentNode, IProjectResource resource);
        MagitekResult<ResourceNode> CreateNewFolder(ResourceNode parentNode, string name);
        MagitekResult CanMoveNode(ResourceNode node, ResourceNode parentNode);
        MagitekResult MoveNode(ResourceNode node, ResourceNode parentNode);

        MagitekResult ApplyResourceDeletionChanges(IList<ResourceChange> changes, Palette defaultPalette);
        IEnumerable<ResourceChange> PreviewResourceDeletionChanges(ResourceNode deleteNode);
        MagitekResult RenameResource(ResourceNode node, string newName);

        ProjectTree GetContainingProject(ResourceNode node);
        ProjectTree GetContainingProject(IProjectResource resource);
        bool AreResourcesInSameProject(IProjectResource a, IProjectResource b);
    }
}
