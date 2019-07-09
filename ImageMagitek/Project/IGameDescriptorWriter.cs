using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorWriter
    {
        void WriteProject(IDictionary<string, ProjectResourceBase> projectTree, string fileName);
    }
}
