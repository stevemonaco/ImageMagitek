using System;
using System.Collections.Generic;
using System.Text;
using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public class ProjectTree
    {
        public IPathTree<IProjectResource> Tree { get; }
        public ImageProject Project => Tree?.Root?.Value as ImageProject;

        public ProjectTree(IPathTree<IProjectResource> projectTree)
        {
            if (projectTree.Root.Value is ImageProject)
                Tree = projectTree;
            else
                throw new ArgumentException($"{nameof(ProjectTree)} ctor called with invalid" + 
                    $"'{nameof(projectTree)}' with root type '{projectTree.Root.Value.GetType()}'");
        }
    }
}
