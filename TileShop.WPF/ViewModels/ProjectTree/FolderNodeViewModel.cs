using System.Collections.Generic;
using Stylet;
using Monaco.PathTree;
using ImageMagitek.Project;
using ImageMagitek.Colors;
using ImageMagitek;
using System.Linq;

namespace TileShop.WPF.ViewModels
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
