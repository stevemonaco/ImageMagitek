namespace ImageMagitek.Project.Serialization;

public interface IProjectReader
{
    string Version { get; }
    MagitekResults<ProjectTree> ReadProject(string projectFileName);
}
