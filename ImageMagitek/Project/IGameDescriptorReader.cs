using System;
using System.Collections.Generic;
using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorReader
    {
        PathTree<ProjectResourceBase> ReadProject(string fileName, string baseDirectory);
    }
}
