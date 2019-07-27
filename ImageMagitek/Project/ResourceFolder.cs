using System.Collections.Generic;

namespace ImageMagitek.Project
{
    public class ResourceFolder: IProjectResource
    {
        public ResourceFolder() : this("") { }

        public ResourceFolder(string name)
        {
            Name = name;
        }

        public string Name { get; set; }

        public bool CanContainChildResources => true;

        public bool ShouldBeSerialized { get; set; } = true;

        public IEnumerable<IProjectResource> LinkedResources()
        {
            yield break;
        }

        public void Rename(string name)
        {
            Name = name;
        }
    }
}
