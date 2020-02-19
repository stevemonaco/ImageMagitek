using ImageMagitek.Project;

namespace TileShop.WPF.ViewModels
{
    public abstract class ResourceEditorBaseViewModel : ToolViewModel
    {
        public IProjectResource Resource { get; protected set; }
    }
}
