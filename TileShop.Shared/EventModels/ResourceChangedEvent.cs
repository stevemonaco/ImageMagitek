using ImageMagitek.Project;

namespace TileShop.Shared.EventModels
{
    public enum ResourceChangeEffect { Project, File }

    public class ResourceChangedEvent
    {
        public IProjectResource Resource { get; }
        public ResourceChangeEffect Effect { get; }

        public ResourceChangedEvent(IProjectResource resource, ResourceChangeEffect effect)
        {
            Resource = resource;
            Effect = effect;
        }
    }
}
