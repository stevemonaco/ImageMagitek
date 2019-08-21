using System;
using System.Collections.Generic;
using Monaco.PathTree;
using ImageMagitek.Project;
using Caliburn.Micro;
using ImageMagitek.Colors;
using ImageMagitek;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeFolderViewModel : Screen
    {
        private IPathTreeNode<IProjectResource> _node;

        public string Name => _node.Name;

        public ProjectTreeFolderViewModel(IPathTreeNode<IProjectResource> node)
        {
            _node = node;
        }

        public IEnumerable<Screen> Children
        {
            get
            {
                foreach (var node in _node.Children())
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
