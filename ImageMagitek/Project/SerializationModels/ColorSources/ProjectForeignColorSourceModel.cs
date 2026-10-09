using ImageMagitek.Colors;

namespace ImageMagitek.Project.Serialization;

public class ProjectForeignColorSourceModel : IColorSourceModel
{
    public IColor Value { get; set; }

    public ProjectForeignColorSourceModel(IColor value)
    {
        Value = value;
    }

    public bool ResourceEquals(IColorSourceModel sourceModel)
    {
        return sourceModel is ProjectForeignColorSourceModel model && model.Value.GetType() == Value.GetType() &&
            model.Value.Color == Value.Color;
    }
}
