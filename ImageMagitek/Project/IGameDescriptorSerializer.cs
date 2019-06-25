using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorSerializer
    {
        void SerializeProject(IDictionary<string, ProjectResourceBase> projectTree, string fileName);
    }
}
