using System.Collections.Generic;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.Project.Serialization;

public interface IProjectSerializerFactory
{
    List<IProjectResource> GlobalResources { get; }
    IMapFormatFactory MapFormatFactory { get; }

    /// <summary>
    /// The resource kinds the reader, writer and <see cref="BuildResources"/> dispatch through
    /// </summary>
    ResourceKindRegistry Kinds { get; }

    IProjectReader CreateReader();
    IProjectWriter CreateWriter(ProjectTree tree);

    /// <summary>
    /// Builds resource models into an existing tree in the given order with the same resolution and failure reasons as a project load
    /// </summary>
    /// <param name="models">Each model with its parent's path key relative to the root and the resource file's location</param>
    MagitekResults BuildResources(ProjectTree tree, IReadOnlyList<(ResourceModel Model, string ParentNodePath, string FileLocation)> models);
}
