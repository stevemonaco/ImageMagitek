using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using Monaco.PathTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TileShop.Shared.ViewModels
{
    public class ImageProjectNodeViewModel : TreeNodeViewModel
    {
        public override int SortPriority => 0;

        public ImageProjectNodeViewModel(IPathTreeNode<IProjectResource> node)
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
