using System;
using System.Collections.Generic;
using ImageMagitek;

namespace ImageMagitek.Project
{
    class ProjectResourceBaseComparer : IComparer<ProjectResourceBase>
    {
        public int Compare(ProjectResourceBase x, ProjectResourceBase y)
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
