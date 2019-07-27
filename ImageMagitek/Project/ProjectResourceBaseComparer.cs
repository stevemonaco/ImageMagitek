using System.Collections.Generic;

namespace ImageMagitek.Project
{
    class ProjectResourceBaseComparer : IComparer<IProjectResource>
    {
        public int Compare(IProjectResource x, IProjectResource y)
        {
            if (x is ResourceFolder && y is ResourceFolder)
                return string.Compare(x.Name, y.Name);
            else if (x is ResourceFolder)
                return -1;
            else if (y is ResourceFolder)
                return 1;
            else
                return string.Compare(x.Name, y.Name);
        }
    }
}
