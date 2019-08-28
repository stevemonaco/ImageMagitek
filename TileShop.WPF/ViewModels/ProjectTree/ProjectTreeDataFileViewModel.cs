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
        public IPathTreeNode<IProjectResource> Node { get; set; }

        public string Name => Node.Name;

        public ProjectTreeDataFileViewModel(IPathTreeNode<IProjectResource> node)
        {
            Node = node;
        } 
    }
}
