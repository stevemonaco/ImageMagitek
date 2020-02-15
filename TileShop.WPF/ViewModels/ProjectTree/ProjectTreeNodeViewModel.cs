using Stylet;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace TileShop.WPF.ViewModels
{
    public abstract class ProjectTreeNodeViewModel : Screen
    {
        public IPathTreeNode<IProjectResource> Node { get; set; }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetAndNotify(ref _name, value);
        }
    }
}
