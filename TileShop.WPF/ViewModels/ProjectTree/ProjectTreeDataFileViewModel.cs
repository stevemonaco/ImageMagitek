using System;
using System.Collections.Generic;
using System.Text;
using Caliburn.Micro;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeDataFileViewModel : Screen
    {
        private IPathTreeNode<IProjectResource> _node;

        public string Name => _node.Name;

        public ProjectTreeDataFileViewModel(IPathTreeNode<IProjectResource> node)
        {
            _node = node;
        } 
    }
}
