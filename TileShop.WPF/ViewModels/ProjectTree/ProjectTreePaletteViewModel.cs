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
        public IPathTreeNode<IProjectResource> Node { get; set; }

        public string Name => Node.Name;

        public ProjectTreePaletteViewModel(IPathTreeNode<IProjectResource> node)
        {
            Node = node;
        }
    }
}
