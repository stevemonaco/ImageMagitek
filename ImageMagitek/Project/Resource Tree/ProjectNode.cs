using System;
using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project;

public sealed class ProjectNode : ResourceNode<ImageProjectModel>
{
    public string BaseDirectory { get; }

    internal event EventHandler<ProjectTreeChange>? TreeChanged;

    public ProjectNode(string baseDirectory, string nodeName, ImageProject resource) : base(nodeName, resource)
    {
        BaseDirectory = baseDirectory;
    }

    internal void OnTreeChanged(ProjectTreeChange change) => TreeChanged?.Invoke(this, change);
}
