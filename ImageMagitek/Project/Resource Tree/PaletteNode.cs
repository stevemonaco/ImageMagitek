using System;
using System.Collections.Generic;
using ImageMagitek.Colors;
using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project;

public sealed class PaletteNode : ResourceNode<PaletteModel>
{
    public PaletteNode(string nodeName, Palette resource) : base(nodeName, resource)
    {
    }

    /// <summary>
    /// Set while the palette has pending edits, so writers serialize the committed state instead of the live palette
    /// </summary>
    public Func<Dictionary<IProjectResource, string>, PaletteModel>? CommittedModel { get; set; }
}
