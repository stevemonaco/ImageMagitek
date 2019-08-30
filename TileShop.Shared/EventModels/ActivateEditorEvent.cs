using ImageMagitek.Project;

namespace TileShop.Shared.EventModels
{
    public class ActivateEditorEvent
    {
        public IProjectResource Resource { get; set; }

        public ActivateEditorEvent(IProjectResource resource)
        {
            Resource = resource;
        }
    }
}
