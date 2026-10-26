using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Metatiles;

namespace ImageMagitek.Project.Serialization;

internal sealed class MetatileSetKind : ResourceKind<MetatileSet, MetatileSetModel, MetatileSetNode>
{
    public override string ElementName => "metatileset";
    public override Version MinimumVersion => ProjectFormat.Current;

    public override ResourceNode CreateNode(IProjectResource resource) => new MetatileSetNode(resource.Name, (MetatileSet)resource);

    internal override ResourceModel? MapToModel(ResourceNode node, Dictionary<IProjectResource, string> resourceMap, IColorFactory colorFactory) =>
        node.Item is MetatileSet metatileSet ? metatileSet.MapToModel(resourceMap) : null;

    internal override ResourceModel Read(XElement element, ResourceReadContext context) =>
        new MetatileSetModel
        {
            Name = context.Name,
            MetatileDataKey = element.Attribute("metatiledatafile")!.Value,
            MetatileOffset = ParseHex(element.Attribute("metatileoffset")!.Value),
            MetatileCount = int.Parse(element.Attribute("metatilecount")!.Value),
            MetatileWidth = int.Parse(element.Attribute("metatilewidth")?.Value ?? "2"),
            MetatileHeight = int.Parse(element.Attribute("metatileheight")?.Value ?? "2"),
            Layers = int.Parse(element.Attribute("layers")?.Value ?? "1"),
            FormatName = element.Attribute("format")!.Value,
            Order = MetatileOrderExtensions.FromXmlName(element.Attribute("order")?.Value),
            TileDataKey = element.Attribute("tiledatafile")!.Value,
            TileOffset = ParseHex(element.Attribute("tileoffset")!.Value),
            CodecName = element.Attribute("codec")!.Value,
            FirstTile = int.Parse(element.Attribute("firsttile")?.Value ?? "0"),
            TileCount = element.Attribute("tilecount") is { } tileCount ? int.Parse(tileCount.Value) : null,
            PaletteSlots = element.Elements("paletteslot")
                .ToDictionary(x => int.Parse(x.Attribute("index")!.Value), x => x.Attribute("palette")!.Value),
            AttributeDataKey = element.Attribute("attributedatafile")?.Value,
            AttributeOffset = element.Attribute("attributeoffset") is { } attributeOffset ? ParseHex(attributeOffset.Value) : 0,
            AttributeSize = element.Attribute("attributesize") is { } attributeSize ? int.Parse(attributeSize.Value) : null
        };

    private static long ParseHex(string value) => long.Parse(value, System.Globalization.NumberStyles.HexNumber);

    internal override XElement Write(ResourceModel resourceModel, string resourceBaseDirectory, IColorFactory colorFactory)
    {
        var model = (MetatileSetModel)resourceModel;
        var element = new XElement(ElementName);
        element.Add(new XAttribute("metatiledatafile", model.MetatileDataKey));
        element.Add(new XAttribute("metatileoffset", $"{model.MetatileOffset:X}"));
        element.Add(new XAttribute("metatilecount", model.MetatileCount));

        if (model.MetatileWidth != 2)
            element.Add(new XAttribute("metatilewidth", model.MetatileWidth));

        if (model.MetatileHeight != 2)
            element.Add(new XAttribute("metatileheight", model.MetatileHeight));

        if (model.Layers != 1)
            element.Add(new XAttribute("layers", model.Layers));

        element.Add(new XAttribute("format", model.FormatName));

        if (model.Order != MetatileOrder.Interleaved)
            element.Add(new XAttribute("order", model.Order.ToXmlName()));

        element.Add(new XAttribute("tiledatafile", model.TileDataKey));
        element.Add(new XAttribute("tileoffset", $"{model.TileOffset:X}"));
        element.Add(new XAttribute("codec", model.CodecName));

        if (model.FirstTile != 0)
            element.Add(new XAttribute("firsttile", model.FirstTile));

        if (model.TileCount is { } tileCount)
            element.Add(new XAttribute("tilecount", tileCount));

        if (model.AttributeDataKey is { } attributeKey)
        {
            element.Add(new XAttribute("attributedatafile", attributeKey));
            element.Add(new XAttribute("attributeoffset", $"{model.AttributeOffset:X}"));

            if (model.AttributeSize is { } attributeSize)
                element.Add(new XAttribute("attributesize", attributeSize));
        }

        foreach (var (index, paletteKey) in model.PaletteSlots.OrderBy(x => x.Key))
            element.Add(new XElement("paletteslot", new XAttribute("index", index), new XAttribute("palette", paletteKey)));

        return element;
    }

    internal override MagitekResults Build(ProjectTreeBuilder builder, ResourceModel model, string parentNodePath, string fileLocation) =>
        builder.AddMetatileSet((MetatileSetModel)model, parentNodePath, fileLocation);

    public override ResourceModel WithKeys(ResourceModel model, string name, Func<string, string> map)
    {
        var x = (MetatileSetModel)model;
        return x with
        {
            Name = name,
            MetatileDataKey = map(x.MetatileDataKey),
            TileDataKey = map(x.TileDataKey),
            PaletteSlots = x.PaletteSlots.ToDictionary(slot => slot.Key, slot => map(slot.Value)),
            AttributeDataKey = x.AttributeDataKey is null ? null : map(x.AttributeDataKey)
        };
    }

    internal override string Summarize(ResourceModel model) => $"{((MetatileSetModel)model).MetatileCount} metatiles";

    internal override IEnumerable<long> Offsets(ResourceModel model)
    {
        var set = (MetatileSetModel)model;
        return set.AttributeDataKey is null ? [set.MetatileOffset, set.TileOffset] : [set.MetatileOffset, set.TileOffset, set.AttributeOffset];
    }

    internal override IEnumerable<string> PaletteKeys(ResourceModel model) =>
        ((MetatileSetModel)model).PaletteSlots.Select(x => $"{x.Key}:{x.Value}");
}
