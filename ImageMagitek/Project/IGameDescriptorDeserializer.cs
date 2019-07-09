using System;
using System.Collections.Generic;
using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorDeserializer
    {
        PathTree<ProjectResourceBase> DeserializeProject(string fileName, string baseDirectory);
    }
}
