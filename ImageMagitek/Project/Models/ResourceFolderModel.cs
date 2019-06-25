using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Project.Models
{
    public class ResourceFolderModel
    {
        public string Name { get; set; }

        public ResourceFolder ToResourceFolder()
        {
            var folder = new ResourceFolder();
            folder.Rename(Name);
            return folder;
        }
    }
}
