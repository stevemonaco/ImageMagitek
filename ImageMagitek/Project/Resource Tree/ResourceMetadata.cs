using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project
{
    public abstract record ResourceMetadata
    {
        public virtual ResourceModel Model { get; }
        public string FileLocation { get; init; }
    }
}
