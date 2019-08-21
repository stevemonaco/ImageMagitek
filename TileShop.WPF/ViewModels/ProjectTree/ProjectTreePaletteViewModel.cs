using Caliburn.Micro;
using ImageMagitek.Project;
using Monaco.PathTree;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreePaletteViewModel : Screen
    {
        private IPathTreeNode<IProjectResource> _node;

        public string Name => _node.Name;

        public ProjectTreePaletteViewModel(IPathTreeNode<IProjectResource> node)
        {
            _node = node;
        }
    }
}
