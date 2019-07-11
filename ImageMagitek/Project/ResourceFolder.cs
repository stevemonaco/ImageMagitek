using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using MoreLinq;
using ImageMagitek;

namespace ImageMagitek.Project
{
    public class ResourceFolder: ProjectResourceBase
    {
        public ResourceFolder() : this("") { }

        public ResourceFolder(string name)
        {
            Name = name;
            CanContainChildResources = true;
        }

        public override IEnumerable<ProjectResourceBase> LinkedResources()
        {
            yield break;
        }
    }
}
