using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project
{
    public record ProjectMetadata : ResourceMetadata
    {
        public override ImageProjectModel Model { get; }

        public ProjectMetadata(ImageProjectModel model, string fileLocation)
        {
            Model = model;
            FileLocation = fileLocation;
        }
    }
}
