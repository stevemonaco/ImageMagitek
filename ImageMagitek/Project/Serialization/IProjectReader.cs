namespace ImageMagitek.Project.Serialization;

public interface IProjectReader
{
    MagitekResults<ProjectTree> ReadProject(string projectFileName);
}
