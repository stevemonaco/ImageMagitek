using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Project.SerializationModels
{
    internal class ResourceFolderModel : ProjectNodeModel
    {
        public ResourceFolder ToResourceFolder()
        {
            var folder = new ResourceFolder(Name);
            return folder;
        }

        public static ResourceFolderModel FromResourceFolder(ResourceFolder folder)
        {
            return new ResourceFolderModel()
            {
                Name = folder.Name
            };
        }
    }
}
