using System.Collections.Generic;
using Stylet;
using Monaco.PathTree;
using ImageMagitek.Project;
using ImageMagitek.Colors;
using ImageMagitek;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeFolderViewModel : Screen
    {
        public IPathTreeNode<IProjectResource> Node { get; set; }

        public string Name => Node.Name;

        public ProjectTreeFolderViewModel(IPathTreeNode<IProjectResource> node)
        {
            Node = node;
        }

        public IEnumerable<Screen> Children
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
