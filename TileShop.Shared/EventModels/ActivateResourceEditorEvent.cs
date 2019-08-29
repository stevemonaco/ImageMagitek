using ImageMagitek.Project;

namespace TileShop.Shared.EventModels
{
    public class ActivateResourceEditorEvent
    {
        public IProjectResource Resource { get; set; }

        public ActivateResourceEditorEvent(IProjectResource resource)
        {
            Resource = resource;
        }
    }
}
