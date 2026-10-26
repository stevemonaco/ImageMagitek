using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.Project.Serialization;

internal sealed class TilemapKind : ResourceKind<TilemapArranger, TilemapModel, TilemapNode>
{
    public override string ElementName => "tilemap";
    public override Version MinimumVersion => ProjectFormat.Current;
    public override bool Exportable => true;

    public override ResourceNode CreateNode(IProjectResource resource) => new TilemapNode(resource.Name, (TilemapArranger)resource);

    internal override ResourceModel? MapToModel(ResourceNode node, Dictionary<IProjectResource, string> resourceMap, IColorFactory colorFactory) =>
        node.Item is TilemapArranger tilemap ? tilemap.MapToModel(resourceMap) : null;

    internal override ResourceModel Read(XElement element, ResourceReadContext context) =>
        new TilemapModel
        {
            Name = context.Name,
            MapDataKey = element.Attribute("mapdatafile")!.Value,
            MapOffset = long.Parse(element.Attribute("mapoffset")!.Value, System.Globalization.NumberStyles.HexNumber),
            Width = int.Parse(element.Attribute("width")!.Value),
            Height = int.Parse(element.Attribute("height")!.Value),
            Layout = MapScreenLayoutExtensions.FromXmlName(element.Attribute("layout")?.Value),
            FormatName = element.Attribute("format")!.Value,
            TileDataKey = element.Attribute("tiledatafile")!.Value,
            TileOffset = long.Parse(element.Attribute("tileoffset")!.Value, System.Globalization.NumberStyles.HexNumber),
            CodecName = element.Attribute("codec")!.Value,
            FirstTile = int.Parse(element.Attribute("firsttile")?.Value ?? "0"),
            EmptyTile = int.Parse(element.Attribute("emptytile")?.Value ?? "0"),
            TileCount = element.Attribute("tilecount") is { } tileCount ? int.Parse(tileCount.Value) : null,
            TilePaletteKey = element.Attribute("tilepalette")?.Value,
            PaletteSlots = element.Elements("paletteslot")
                .ToDictionary(x => int.Parse(x.Attribute("index")!.Value), x => x.Attribute("palette")!.Value)
        };

    internal override XElement Write(ResourceModel resourceModel, string resourceBaseDirectory, IColorFactory colorFactory)
    {
        var model = (TilemapModel)resourceModel;
        var element = new XElement(ElementName);
        element.Add(new XAttribute("mapdatafile", model.MapDataKey));
        element.Add(new XAttribute("mapoffset", $"{model.MapOffset:X}"));
        element.Add(new XAttribute("width", model.Width));
        element.Add(new XAttribute("height", model.Height));

        if (model.Layout != MapScreenLayout.Linear)
            element.Add(new XAttribute("layout", model.Layout.ToXmlName()));

        element.Add(new XAttribute("format", model.FormatName));
        element.Add(new XAttribute("tiledatafile", model.TileDataKey));
        element.Add(new XAttribute("tileoffset", $"{model.TileOffset:X}"));
        element.Add(new XAttribute("codec", model.CodecName));

        if (model.FirstTile != 0)
            element.Add(new XAttribute("firsttile", model.FirstTile));

        if (model.EmptyTile != 0)
            element.Add(new XAttribute("emptytile", model.EmptyTile));

        if (model.TileCount is { } tileCount)
            element.Add(new XAttribute("tilecount", tileCount));

        if (model.TilePaletteKey is not null)
            element.Add(new XAttribute("tilepalette", model.TilePaletteKey));

        foreach (var (index, paletteKey) in model.PaletteSlots.OrderBy(x => x.Key))
            element.Add(new XElement("paletteslot", new XAttribute("index", index), new XAttribute("palette", paletteKey)));

        return element;
    }

    internal override MagitekResults Build(ProjectTreeBuilder builder, ResourceModel model, string parentNodePath, string fileLocation) =>
        builder.AddTilemap((TilemapModel)model, parentNodePath, fileLocation);

    public override ResourceModel WithKeys(ResourceModel model, string name, Func<string, string> map)
    {
        var x = (TilemapModel)model;
        return x with
        {
            Name = name,
            MapDataKey = map(x.MapDataKey),
            TileDataKey = map(x.TileDataKey),
            TilePaletteKey = x.TilePaletteKey is null ? null : map(x.TilePaletteKey),
            PaletteSlots = x.PaletteSlots.ToDictionary(slot => slot.Key, slot => map(slot.Value))
        };
    }

    internal override string Summarize(ResourceModel model)
    {
        var tilemap = (TilemapModel)model;
        return $"{tilemap.Width}x{tilemap.Height} tiles";
    }

    internal override IEnumerable<long> Offsets(ResourceModel model)
    {
        var tilemap = (TilemapModel)model;
        return [tilemap.MapOffset, tilemap.TileOffset];
    }

    internal override IEnumerable<string> PaletteKeys(ResourceModel model) =>
        ((TilemapModel)model).PaletteSlots.Select(x => $"{x.Key}:{x.Value}");
}
