using ImageMagitek.Project;
using Stylet;
using TileShop.Shared.EventModels;

namespace TileShop.WPF.ViewModels
{
    public abstract class ResourceEditorBaseViewModel : ToolViewModel, IHandle<ResourceRenamedEvent>
    {
        public IProjectResource Resource { get; protected set; }

        public virtual void Handle(ResourceRenamedEvent message)
        {
            if (ReferenceEquals(Resource, message.Resource))
                DisplayName = message.NewName;
        }
    }
}
