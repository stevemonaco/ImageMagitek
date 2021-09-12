using ImageMagitek.Project;

namespace TileShop.Shared.EventModels
{
    public record ResourceRenamedEvent(IProjectResource Resource, string NewName, string OldName);
}
