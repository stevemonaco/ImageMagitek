using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using ImageMagitek.Colors;

namespace ImageMagitek.Project.Serialization;

internal sealed class ScatteredArrangerKind : ResourceKind<ScatteredArranger, ScatteredArrangerModel, ArrangerNode>
{
    public override string ElementName => "arranger";
    public override bool Exportable => true;

    public override ResourceNode CreateNode(IProjectResource resource) => new ArrangerNode(resource.Name, (ScatteredArranger)resource);

    internal override ResourceModel? MapToModel(ResourceNode node, Dictionary<IProjectResource, string> resourceMap, IColorFactory colorFactory) =>
        node.Item is ScatteredArranger arranger ? arranger.MapToModel(resourceMap) : null;

    internal override ResourceModel Read(XElement element, ResourceReadContext context)
    {
        var resourceName = context.Name;
        var elementsx = int.Parse(element.Attribute("elementsx")!.Value); // Width of arranger in elements
        var elementsy = int.Parse(element.Attribute("elementsy")!.Value); // Height of arranger in elements
        var width = int.Parse(element.Attribute("width")!.Value); // Width of element in pixels
        var height = int.Parse(element.Attribute("height")!.Value); // Height of element in pixels
        var defaultCodecName = element.Attribute("defaultcodec")!.Value;
        var defaultDataFileKey = element.Attribute("defaultdatafile")!.Value;
        var defaultPaletteKey = element.Attribute("defaultpalette")?.Value ?? context.DefaultPalette.Name;
        var layoutName = element.Attribute("layout")!.Value;
        var colorTypeName = element.Attribute("color")?.Value ?? "indexed";
        var elementList = element.Descendants("element");

        var layout = layoutName switch
        {
            "tiled" => ElementLayout.Tiled,
            "single" => ElementLayout.Single,
            _ => throw new XmlException($"Unsupported arranger layout type ('{layoutName}') for arranger '{resourceName}'")
        };

        var colorType = colorTypeName switch
        {
            "indexed" => PixelColorType.Indexed,
            "direct" => PixelColorType.Direct,
            _ => throw new XmlException($"Unsupported pixel color type ('{colorTypeName}') for arranger '{resourceName}'")
        };

        var xmlElements = elementList.Select(e => new
        {
            fileoffset = long.Parse(e.Attribute("fileoffset")!.Value, System.Globalization.NumberStyles.HexNumber),
            bitoffset = e.Attribute("bitoffset"),
            posx = int.Parse(e.Attribute("posx")!.Value),
            posy = int.Parse(e.Attribute("posy")!.Value),
            format = e.Attribute("codec"),
            palette = e.Attribute("palette"),
            datafile = e.Attribute("datafile"),
            mirror = e.Attribute("mirror"),
            rotation = e.Attribute("rotation")
        });

        var grid = new ArrangerElementModel[elementsx, elementsy];

        foreach (var xmlElement in xmlElements)
        {
            grid[xmlElement.posx, xmlElement.posy] = new ArrangerElementModel()
            {
                DataFileKey = xmlElement.datafile?.Value ?? defaultDataFileKey,
                PaletteKey = colorType == PixelColorType.Direct ? null : xmlElement.palette?.Value ?? defaultPaletteKey,
                CodecName = xmlElement.format?.Value ?? defaultCodecName,
                PositionX = xmlElement.posx,
                PositionY = xmlElement.posy,
                FileAddress = new BitAddress(xmlElement.fileoffset, xmlElement.bitoffset is null ? 0 : int.Parse(xmlElement.bitoffset.Value)),
                Mirror = xmlElement.mirror?.Value switch
                {
                    "none" => MirrorOperation.None,
                    "horizontal" => MirrorOperation.Horizontal,
                    "vertical" => MirrorOperation.Vertical,
                    "both" => MirrorOperation.Both,
                    _ => MirrorOperation.None
                },
                Rotation = xmlElement.rotation?.Value switch
                {
                    "none" => RotationOperation.None,
                    "left" => RotationOperation.Left,
                    "right" => RotationOperation.Right,
                    "turn" => RotationOperation.Turn,
                    _ => RotationOperation.None
                }
            };
        }

        return new ScatteredArrangerModel()
        {
            Name = resourceName,
            ElementGrid = grid,
            ArrangerElementSize = new Size(elementsx, elementsy),
            ElementPixelSize = new Size(width, height),
            Layout = layout,
            ColorType = colorType
        };
    }

    internal override XElement Write(ResourceModel model, string resourceBaseDirectory, IColorFactory colorFactory)
    {
        var arrangerModel = (ScatteredArrangerModel)model;
        var mostUsedCodecName = arrangerModel.FindMostFrequentElementPropertyValue(x => x.CodecName);
        var mostUsedFileKey = arrangerModel.FindMostFrequentElementPropertyValue(x => x.DataFileKey);
        var mostUsedPaletteKey = arrangerModel.FindMostFrequentElementPropertyValue(x => x.PaletteKey);

        var arrangerNode = new XElement(ElementName);
        arrangerNode.Add(new XAttribute("elementsx", arrangerModel.ArrangerElementSize.Width));
        arrangerNode.Add(new XAttribute("elementsy", arrangerModel.ArrangerElementSize.Height));
        arrangerNode.Add(new XAttribute("width", arrangerModel.ElementPixelSize.Width));
        arrangerNode.Add(new XAttribute("height", arrangerModel.ElementPixelSize.Height));

        if (arrangerModel.Layout == ElementLayout.Tiled)
            arrangerNode.Add(new XAttribute("layout", "tiled"));
        else if (arrangerModel.Layout == ElementLayout.Single)
            arrangerNode.Add(new XAttribute("layout", "single"));

        if (arrangerModel.ColorType == PixelColorType.Indexed)
            arrangerNode.Add(new XAttribute("color", "indexed"));
        else if (arrangerModel.ColorType == PixelColorType.Direct)
            arrangerNode.Add(new XAttribute("color", "direct"));

        arrangerNode.Add(new XAttribute("defaultcodec", mostUsedCodecName ?? ""));
        arrangerNode.Add(new XAttribute("defaultdatafile", mostUsedFileKey ?? ""));

        if (arrangerModel.ColorType == PixelColorType.Indexed)
            arrangerNode.Add(new XAttribute("defaultpalette", mostUsedPaletteKey ?? ""));

        for (int y = 0; y < arrangerModel.ArrangerElementSize.Height; y++)
        {
            for (int x = 0; x < arrangerModel.ArrangerElementSize.Width; x++)
            {
                var el = arrangerModel.ElementGrid[x, y];

                if (el is null)
                    continue;

                var elNode = new XElement("element");
                elNode.Add(new XAttribute("fileoffset", $"{el.FileAddress.ByteOffset:X}"));
                elNode.Add(new XAttribute("posx", el.PositionX));
                elNode.Add(new XAttribute("posy", el.PositionY));

                if (el.FileAddress.BitOffset != 0)
                    elNode.Add(new XAttribute("bitoffset", el.FileAddress.BitOffset));

                if (el.CodecName != mostUsedCodecName)
                    elNode.Add(new XAttribute("codec", el.CodecName));

                if (el.DataFileKey != mostUsedFileKey)
                    elNode.Add(new XAttribute("datafile", el.DataFileKey));

                if (el.PaletteKey != mostUsedPaletteKey && arrangerModel.ColorType == PixelColorType.Indexed)
                    elNode.Add(new XAttribute("palette", el.PaletteKey!));

                if (el.Mirror == MirrorOperation.Horizontal)
                    elNode.Add(new XAttribute("mirror", "horizontal"));
                else if (el.Mirror == MirrorOperation.Vertical)
                    elNode.Add(new XAttribute("mirror", "vertical"));
                else if (el.Mirror == MirrorOperation.Both)
                    elNode.Add(new XAttribute("mirror", "both"));

                if (el.Rotation == RotationOperation.Left)
                    elNode.Add(new XAttribute("rotation", "left"));
                else if (el.Rotation == RotationOperation.Right)
                    elNode.Add(new XAttribute("rotation", "right"));
                else if (el.Rotation == RotationOperation.Turn)
                    elNode.Add(new XAttribute("rotation", "turn"));

                arrangerNode.Add(elNode);
            }
        }

        return arrangerNode;
    }

    internal override MagitekResults Build(ProjectTreeBuilder builder, ResourceModel model, string parentNodePath, string fileLocation) =>
        builder.AddScatteredArranger((ScatteredArrangerModel)model, parentNodePath, fileLocation);

    public override ResourceModel WithKeys(ResourceModel model, string name, Func<string, string> map)
    {
        var x = (ScatteredArrangerModel)model;
        return x with { Name = name, ElementGrid = MapGrid(x.ElementGrid, map) };
    }

    internal override string Summarize(ResourceModel model)
    {
        var arranger = (ScatteredArrangerModel)model;
        return $"{arranger.ArrangerElementSize.Width}x{arranger.ArrangerElementSize.Height} elements";
    }

    internal override IEnumerable<long> Offsets(ResourceModel model) =>
        ((ScatteredArrangerModel)model).EnumerateElements().OfType<ArrangerElementModel>().Select(x => x.FileAddress.ByteOffset * 8 + x.FileAddress.BitOffset);

    internal override IEnumerable<string> PaletteKeys(ResourceModel model) =>
        ((ScatteredArrangerModel)model).EnumerateElements().OfType<ArrangerElementModel>().Select(x => x.PaletteKey ?? "");

    private static ArrangerElementModel[,] MapGrid(ArrangerElementModel[,] grid, Func<string, string> map)
    {
        var mapped = new ArrangerElementModel[grid.GetLength(0), grid.GetLength(1)];

        for (var y = 0; y < grid.GetLength(1); y++)
        {
            for (var x = 0; x < grid.GetLength(0); x++)
            {
                if (grid[x, y] is not { } el)
                    continue;

                mapped[x, y] = el with { DataFileKey = map(el.DataFileKey), PaletteKey = el.PaletteKey is null ? null : map(el.PaletteKey) };
            }
        }

        return mapped;
    }
}
