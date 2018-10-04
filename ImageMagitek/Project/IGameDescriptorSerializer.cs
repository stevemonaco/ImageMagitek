using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImageMagitek.Project
{
    interface IGameDescriptorSerializer
    {
        IDictionary<string, ProjectResourceBase> DeserializeProject(string fileName, string baseDirectory);
        void SerializeProject(IDictionary<string, ProjectResourceBase> projectTree, string fileName);
    }
}
