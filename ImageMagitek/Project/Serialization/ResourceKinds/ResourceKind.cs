using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using ImageMagitek.Colors;

namespace ImageMagitek.Project.Serialization;

/// <summary>
/// Describes one resource kind once: its XML element, resource, model and node types, how it is read, written, mapped and built,
/// and the per-kind facets regeneration uses (LIB-PROJECT-FORMAT-075)
/// </summary>
public abstract class ResourceKind
{
    /// <summary>
    /// The resource file's root element name, which is also the kind's table manifest type
    /// </summary>
    public abstract string ElementName { get; }

    /// <summary>
    /// The resource type; subclasses of it resolve to this kind unless another kind names them
    /// </summary>
    public abstract Type ResourceType { get; }

    public abstract Type ModelType { get; }
    public abstract Type NodeType { get; }

    /// <summary>
    /// The lowest project format version that can hold a resource of this kind
    /// </summary>
    public virtual Version MinimumVersion => ProjectFormat.Minimum;

    /// <summary>
    /// True when the CLI exports and imports resources of this kind as images
    /// </summary>
    public virtual bool Exportable => false;

    internal virtual bool BuildsInPasses => false;

    /// <summary>
    /// Creates an unattached node over <paramref name="resource"/>, which must be of <see cref="ResourceType"/>
    /// </summary>
    public abstract ResourceNode CreateNode(IProjectResource resource);

    internal abstract ResourceModel? MapToModel(ResourceNode node, Dictionary<IProjectResource, string> resourceMap, IColorFactory colorFactory);

    internal virtual string? FindUnwritableReason(ResourceModel model, IColorFactory colorFactory) => null;

    internal abstract ResourceModel? Read(XElement root, ResourceReadContext context);

    internal abstract XElement Write(ResourceModel model, string resourceBaseDirectory, IColorFactory colorFactory);

    internal abstract MagitekResults Build(ProjectTreeBuilder builder, ResourceModel model, string parentNodePath, string fileLocation);

    /// <summary>
    /// Copies <paramref name="model"/> with the name <paramref name="name"/> and every key it references passed through <paramref name="map"/>
    /// </summary>
    public virtual ResourceModel WithKeys(ResourceModel model, string name, Func<string, string> map) =>
        throw new NotSupportedException($"Regenerating a '{model.GetType().Name}' is not supported");

    internal virtual string Summarize(ResourceModel model) => "";

    internal virtual IEnumerable<long> Offsets(ResourceModel model) => [];

    internal virtual IEnumerable<string> PaletteKeys(ResourceModel model) => [];
}

internal abstract class ResourceKind<TResource, TModel, TNode> : ResourceKind
    where TResource : IProjectResource
    where TModel : ResourceModel
    where TNode : ResourceNode
{
    public override Type ResourceType => typeof(TResource);
    public override Type ModelType => typeof(TModel);
    public override Type NodeType => typeof(TNode);
}

/// <summary>
/// What a kind's reader needs: the resource file, the directory data file locations are relative to, and where to report errors
/// </summary>
internal sealed record ResourceReadContext(string FileName, string BaseDirectory, IColorFactory ColorFactory, Palette DefaultPalette, List<string> Errors)
{
    public string Name => Path.GetFileNameWithoutExtension(FileName);
}
