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
        public ResourceFolder()
        {
            CanContainChildResources = true;
        }

        public override void Rename(string name)
        {
            Name = name;
        }

        public override ProjectResourceBase Clone()
        {
            ResourceFolder rf = new ResourceFolder();
            rf.Name = Name;

            return rf;
        }
    }
}
