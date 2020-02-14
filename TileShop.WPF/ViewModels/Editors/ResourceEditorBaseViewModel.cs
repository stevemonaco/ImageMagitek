using ImageMagitek.Project;

namespace TileShop.WPF.ViewModels
{
    public abstract class ResourceEditorBaseViewModel : EditorBaseViewModel
    {
        public override string Name => Resource?.Name;
        public IProjectResource Resource { get; protected set; }

        public abstract bool SaveChanges();
        public abstract bool DiscardChanges();
    }
}
