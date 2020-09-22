namespace ImageMagitek.Project
{
    public class FolderNode : ResourceNode<ResourceFolder>
    {
        public ResourceFolder Folder { get; }

        public FolderNode(string name, ResourceFolder folder) : base(name, folder)
        {
            Folder = folder;
        }
    }
}
