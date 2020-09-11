using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;

namespace TileShop.WPF.ViewModels
{
    public class ProjectNodeViewModel : ResourceNodeViewModel
    {
        public override int SortPriority => 0;

        public ProjectNodeViewModel(ResourceNode node)
        {
            Node = node;
            Name = node.Name;

            foreach (var child in Node.Children)
            {
                ResourceNodeViewModel model;

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
