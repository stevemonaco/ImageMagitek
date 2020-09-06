using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;

namespace TileShop.WPF.ViewModels
{
    public class ImageProjectNodeViewModel : TreeNodeViewModel
    {
        public override int SortPriority => 0;

        public ImageProjectNodeViewModel(ResourceNode node)
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
        }
    }
}
