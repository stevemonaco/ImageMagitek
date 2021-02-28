namespace ImageMagitek.Project.Serialization
{
    public class ImageProjectModel : ResourceModel
    {
        public string Root { get; set; }

        public ImageProject ToImageProject()
        {
            return new ImageProject()
            {
                Name = Name,
                Root = Root
            };
        }

        public static ImageProjectModel FromImageProject(ImageProject project)
        {
            return new ImageProjectModel()
            {
                Name = project.Name,
                Root = project.Root
            };
        }
    }
}
