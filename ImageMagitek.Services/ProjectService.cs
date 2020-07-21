using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ImageMagitek.Project;

namespace ImageMagitek.Services
{
    public interface IProjectService
    { 
    }

    public class ProjectService
    {
        public Dictionary<string, IProjectResource> DefaultResources { get; }

        public ProjectService(IEnumerable<IProjectResource> defaultResources)
        {
            DefaultResources = defaultResources.ToDictionary(x => x.Name);
        }
    }
}
