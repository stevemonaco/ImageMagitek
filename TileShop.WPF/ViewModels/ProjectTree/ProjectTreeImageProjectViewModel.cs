using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using Monaco.PathTree;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeImageProjectViewModel : ProjectTreeNodeViewModel
    {
        public ProjectTreeImageProjectViewModel(IPathTreeNode<IProjectResource> node)
        {
            Node = node;
            Name = node.Name;
            Type = GetType();
        }

        public IEnumerable<ProjectTreeNodeViewModel> Children
        {
            get
            {
                foreach (var node in Node.Children())
                {
                    if (node.Value is ResourceFolder)
                        yield return new ProjectTreeFolderViewModel(node);
                    else if (node.Value is Palette)
                        yield return new ProjectTreePaletteViewModel(node);
                    else if (node.Value is DataFile)
                        yield return new ProjectTreeDataFileViewModel(node);
                    else if (node.Value is Arranger)
                        yield return new ProjectTreeArrangerViewModel(node);
                }
            }
        }
    }
}
