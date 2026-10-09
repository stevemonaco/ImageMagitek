namespace ImageMagitek.Project.Serialization;

public abstract class ResourceModel
{
    public abstract required string Name { get; init; }

    public abstract bool ResourceEquals(ResourceModel? model);
}
