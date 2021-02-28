namespace ImageMagitek.Project.Serialization
{
    internal class ResourceFolderModel : ResourceModel
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
