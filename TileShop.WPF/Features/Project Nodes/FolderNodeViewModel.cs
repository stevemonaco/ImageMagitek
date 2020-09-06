using ImageMagitek;
using ImageMagitek.Project;
using ImageMagitek.Colors;

namespace TileShop.WPF.ViewModels
{
    public class FolderNodeViewModel : TreeNodeViewModel
    {
        public override int SortPriority => 1;

        public FolderNodeViewModel(ResourceNode node, TreeNodeViewModel parent)
        {
            Node = node;
            Name = node.Name;
            Type = GetType();

            foreach (var child in Node.Children)
            {
                TreeNodeViewModel model;

                if (child.Value is ResourceFolder)
                    model = new FolderNodeViewModel(child, this);
                else if (child.Value is Palette)
                    model = new PaletteNodeViewModel(child, this);
                else if (child.Value is DataFile)
                    model = new DataFileNodeViewModel(child, this);
                else if (child.Value is Arranger)
                    model = new ArrangerNodeViewModel(child, this);
                else
                    continue;

                Children.Add(model);
            }

            ParentModel = parent;
        }
    }
}
