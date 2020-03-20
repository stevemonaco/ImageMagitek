using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using Monaco.PathTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TileShop.WPF.ViewModels
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
                TreeNodeViewModel model;

                if (child.Value is ResourceFolder)
                    model = new FolderNodeViewModel(child);
                else if (child.Value is Palette)
                    model = new PaletteNodeViewModel(child);
                else if (child.Value is DataFile)
                    model = new DataFileNodeViewModel(child);
                else if (child.Value is Arranger)
                    model = new ArrangerNodeViewModel(child);
                else
                    continue;

                model.ParentModel = this;
                Children.Add(model);
            }
        }
    }
}
