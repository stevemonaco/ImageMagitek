using System.Collections.Generic;

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
