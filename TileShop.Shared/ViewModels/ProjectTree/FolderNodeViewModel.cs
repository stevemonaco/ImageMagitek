using System.Collections.Generic;
using Stylet;
using Monaco.PathTree;
using ImageMagitek.Project;
using ImageMagitek.Colors;
using ImageMagitek;
using System.Linq;

namespace TileShop.Shared.ViewModels
{
    public class FolderNodeViewModel : TreeNodeViewModel
    {
        public override int SortPriority => 1;

        public FolderNodeViewModel(IPathTreeNode<IProjectResource> node)
        {
            Node = node;
            Name = node.Name;
            Type = GetType();

            foreach (var child in Node.Children())
            {
                if (child.Value is ResourceFolder)
                    Children.Add(new FolderNodeViewModel(child));
                else if (child.Value is Palette)
                    Children.Add(new PaletteNodeViewModel(child));
                else if (child.Value is DataFile)
                    Children.Add(new DataFileNodeViewModel(child));
                else if (child.Value is Arranger)
                    Children.Add(new ArrangerNodeViewModel(child));
            }
        }
    }
}
