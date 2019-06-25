using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Project
{
    public interface IGameDescriptorDeserializer
    {
        IDictionary<string, ProjectResourceBase> DeserializeProject(string fileName, string baseDirectory);
    }
}
