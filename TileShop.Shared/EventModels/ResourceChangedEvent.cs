using ImageMagitek.Project;

namespace TileShop.Shared.EventModels
{
    public enum ResourceModifyEffect { Project, File }
    public enum ResourceChangeAction { Modified, Saved, Renamed, Moved }

    public class ResourceChangedEvent
    {
        public IProjectResource Resource { get; }
        public ResourceModifyEffect Effect { get; }

        public ResourceChangedEvent(IProjectResource resource, ResourceModifyEffect effect)
        {
            Resource = resource;
            Effect = effect;
        }
    }
}
