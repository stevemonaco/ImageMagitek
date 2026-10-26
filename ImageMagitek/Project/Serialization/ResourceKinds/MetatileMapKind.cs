using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Metatiles;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.Project.Serialization;

internal sealed class MetatileMapKind : ResourceKind<MetatileMap, MetatileMapModel, MetatileMapNode>
{
    public override string ElementName => "metatilemap";
    public override Version MinimumVersion => ProjectFormat.Current;
    public override bool Exportable => true;

    public override ResourceNode CreateNode(IProjectResource resource) => new MetatileMapNode(resource.Name, (MetatileMap)resource);

    internal override ResourceModel? MapToModel(ResourceNode node, Dictionary<IProjectResource, string> resourceMap, IColorFactory colorFactory) =>
        node.Item is MetatileMap metatileMap ? metatileMap.MapToModel(resourceMap) : null;

    internal override ResourceModel Read(XElement element, ResourceReadContext context) =>
        new MetatileMapModel
        {
            Name = context.Name,
            MapDataKey = element.Attribute("mapdatafile")!.Value,
            MapOffset = long.Parse(element.Attribute("mapoffset")!.Value, System.Globalization.NumberStyles.HexNumber),
            Width = int.Parse(element.Attribute("width")!.Value),
            Height = int.Parse(element.Attribute("height")!.Value),
            Layout = MapScreenLayoutExtensions.FromXmlName(element.Attribute("layout")?.Value),
            FormatName = element.Attribute("format")!.Value,
            EmptyBlock = int.Parse(element.Attribute("emptyblock")?.Value ?? "0"),
            Sets = element.Elements("set")
                .Select(x => (x.Attribute("metatileset")!.Value, int.Parse(x.Attribute("base")!.Value)))
                .ToList()
        };

    internal override XElement Write(ResourceModel resourceModel, string resourceBaseDirectory, IColorFactory colorFactory)
    {
        var model = (MetatileMapModel)resourceModel;
        var element = new XElement(ElementName);
        element.Add(new XAttribute("mapdatafile", model.MapDataKey));
        element.Add(new XAttribute("mapoffset", $"{model.MapOffset:X}"));
        element.Add(new XAttribute("width", model.Width));
        element.Add(new XAttribute("height", model.Height));

        if (model.Layout != MapScreenLayout.Linear)
            element.Add(new XAttribute("layout", model.Layout.ToXmlName()));

        element.Add(new XAttribute("format", model.FormatName));

        if (model.EmptyBlock != 0)
            element.Add(new XAttribute("emptyblock", model.EmptyBlock));

        foreach (var (setKey, setBase) in model.Sets)
            element.Add(new XElement("set", new XAttribute("metatileset", setKey), new XAttribute("base", setBase)));

        return element;
    }

    internal override MagitekResults Build(ProjectTreeBuilder builder, ResourceModel model, string parentNodePath, string fileLocation) =>
        builder.AddMetatileMap((MetatileMapModel)model, parentNodePath, fileLocation);

    public override ResourceModel WithKeys(ResourceModel model, string name, Func<string, string> map)
    {
        var x = (MetatileMapModel)model;
        return x with
        {
            Name = name,
            MapDataKey = map(x.MapDataKey),
            Sets = x.Sets.Select(set => (map(set.SetKey), set.Base)).ToList()
        };
    }

    internal override string Summarize(ResourceModel model)
    {
        var map = (MetatileMapModel)model;
        return $"{map.Width}x{map.Height} blocks";
    }

    internal override IEnumerable<long> Offsets(ResourceModel model) => [((MetatileMapModel)model).MapOffset];
}
