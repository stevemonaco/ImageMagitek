using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Utility.Parsing;

namespace ImageMagitek.Project.Serialization;

internal sealed class PaletteKind : ResourceKind<Palette, PaletteModel, PaletteNode>
{
    public override string ElementName => "palette";

    public override ResourceNode CreateNode(IProjectResource resource) => new PaletteNode(resource.Name, (Palette)resource);

    internal override ResourceModel? MapToModel(ResourceNode node, Dictionary<IProjectResource, string> resourceMap, IColorFactory colorFactory)
    {
        var paletteNode = (PaletteNode)node;
        return paletteNode.CommittedModel?.Invoke(resourceMap) ?? ((Palette)paletteNode.Item).MapToModel(resourceMap, colorFactory);
    }

    internal override string? FindUnwritableReason(ResourceModel resourceModel, IColorFactory colorFactory)
    {
        var model = (PaletteModel)resourceModel;
        if (model.DataFileKey is null)
            return $"Palette '{model.Name}' cannot be saved because it has no data file";

        var foreignType = colorFactory.CreateColor(model.ColorModel).GetType();
        var index = 0;

        foreach (var source in model.ColorSources)
        {
            if (source is ProjectForeignColorSourceModel foreign && foreign.Value.GetType() != foreignType)
                return $"Palette '{model.Name}' cannot be saved because color {index} is not a {model.ColorModel} color";

            index += source switch
            {
                FileColorSourceModel fileSource => fileSource.Entries,
                ScatteredColorSourceModel scatteredSource => scatteredSource.Entries,
                _ => 1
            };
        }

        if (index > Palette.MaxEntries)
            return $"Palette '{model.Name}' cannot be saved because it has {index} colors, more than the {Palette.MaxEntries} a palette can hold";

        return null;
    }

    internal override ResourceModel? Read(XElement element, ResourceReadContext context)
    {
        var xmlFileName = context.FileName;
        var dataFileKey = element.Attribute("datafile")!.Value;
        var colorModel = Palette.StringToColorModel(element.Attribute("color")!.Value);
        var zeroIndexTransparent = bool.Parse(element.Attribute("zeroindextransparent")!.Value);
        var sources = new List<IColorSourceModel>();

        var colorIndex = 0;
        foreach (var item in element.Elements())
        {
            if (item.Name.LocalName == "filesource")
            {
                var fileOffset = long.Parse(item.Attribute("fileoffset")!.Value, System.Globalization.NumberStyles.HexNumber);
                var entries = int.Parse(item.Attribute("entries")!.Value);
                var endian = Endian.Little;
                colorIndex += entries;

                if (item.Attribute("endian") is not null)
                {
                    if (item.Attribute("endian")!.Value == "big")
                        endian = Endian.Big;
                    else if (item.Attribute("endian")!.Value == "little")
                        endian = Endian.Little;
                    else
                        context.Errors.Add($"'endian' has unknown value '{item.Attribute("endian")!.Value}'");
                }

                sources.Add(new FileColorSourceModel(fileOffset, entries, endian));
            }
            else if (item.Name.LocalName == "nativecolor")
            {
                var value = item.Attribute("value")!.Value;
                if (!ColorParser.TryParse(value, ColorModel.Rgba32, out var nativeColor))
                    return FailColor(value, ColorModel.Rgba32);

                sources.Add(new ProjectNativeColorSourceModel((ColorRgba32)nativeColor));
                colorIndex++;
            }
            else if (item.Name.LocalName == "foreigncolor")
            {
                var value = item.Attribute("value")!.Value;
                if (!ColorParser.TryParse(value, colorModel, out var foreignColor))
                    return FailColor(value, colorModel);

                sources.Add(new ProjectForeignColorSourceModel(foreignColor));
                colorIndex++;
            }
            else if (item.Name.LocalName == "scatteredcolor")
            {
                var entries = int.Parse(item.Attribute("entries")!.Value);
                var runs = item.Elements().Select(ReadField).ToList();

                try
                {
                    Palette.ValidateScatteredFields(ScatteredColorSource.ExpandRun(entries, runs), colorModel, context.ColorFactory);
                }
                catch (ColorFieldException ex)
                {
                    context.Errors.Add($"'{xmlFileName}' entry {colorIndex + (ex.Entry ?? 0)} field {ex.Field}: {ex.Reason}");
                    return null;
                }

                sources.Add(new ScatteredColorSourceModel(entries, runs));
                colorIndex += entries;
            }
        }

        if (colorIndex > Palette.MaxEntries)
        {
            context.Errors.Add($"'{xmlFileName}' has {colorIndex} colors, more than the {Palette.MaxEntries} a palette can hold");
            return null;
        }

        return new PaletteModel()
        {
            Name = context.Name,
            DataFileKey = dataFileKey,
            ColorModel = colorModel,
            ZeroIndexTransparent = zeroIndexTransparent,
            ColorSources = sources
        };

        static ColorFieldRun ReadField(XElement field)
        {
            var offset = long.Parse(field.Attribute("fileoffset")!.Value, System.Globalization.NumberStyles.HexNumber);
            var lowBit = int.Parse(field.Attribute("lowbit")?.Value ?? "0");
            var bits = int.Parse(field.Attribute("bits")!.Value);
            var shift = int.Parse(field.Attribute("shift")?.Value ?? "0");
            var stride = int.Parse(field.Attribute("stride")?.Value ?? "8");
            return new ColorFieldRun(new ColorField(offset, lowBit, bits, shift), stride);
        }

        PaletteModel? FailColor(string value, ColorModel colorModel)
        {
            context.Errors.Add($"'{xmlFileName}' entry {colorIndex}: '{value}' is not a valid {colorModel} color");
            return null;
        }
    }

    internal override XElement Write(ResourceModel model, string resourceBaseDirectory, IColorFactory colorFactory)
    {
        var paletteModel = (PaletteModel)model;
        var element = new XElement(ElementName);
        element.Add(new XAttribute("datafile", paletteModel.DataFileKey!));
        element.Add(new XAttribute("color", paletteModel.ColorModel.ToString()));
        element.Add(new XAttribute("zeroindextransparent", paletteModel.ZeroIndexTransparent));

        foreach (var source in paletteModel.ColorSources)
        {
            if (source is FileColorSourceModel fileSource)
            {
                var fileElement = new XElement("filesource");
                fileElement.Add(new XAttribute("fileoffset", $"{fileSource.FileAddress:X}"));

                fileElement.Add(new XAttribute("entries", fileSource.Entries));

                if (fileSource.Endian == Endian.Big)
                    fileElement.Add(new XAttribute("endian", "big"));

                element.Add(fileElement);
            }
            else if (source is ProjectNativeColorSourceModel nativeSource)
            {
                var nativeElement = new XElement("nativecolor");
                var hexColor = colorFactory.ToHexString(nativeSource.Value);
                nativeElement.Add(new XAttribute("value", hexColor));
                element.Add(nativeElement);
            }
            else if (source is ProjectForeignColorSourceModel foreignSource)
            {
                var foreignElement = new XElement("foreigncolor");
                var hexColor = colorFactory.ToHexString(foreignSource.Value);
                foreignElement.Add(new XAttribute("value", hexColor));
                element.Add(foreignElement);
            }
            else if (source is ScatteredColorSourceModel scatteredSource)
            {
                var scatteredElement = new XElement("scatteredcolor");
                scatteredElement.Add(new XAttribute("entries", scatteredSource.Entries));

                foreach (var (field, stride) in scatteredSource.Fields)
                {
                    var fieldElement = new XElement("field");
                    fieldElement.Add(new XAttribute("fileoffset", $"{field.Offset:X}"));

                    if (field.LowBit != 0)
                        fieldElement.Add(new XAttribute("lowbit", field.LowBit));

                    fieldElement.Add(new XAttribute("bits", field.Bits));

                    if (field.Shift != 0)
                        fieldElement.Add(new XAttribute("shift", field.Shift));

                    if (stride != 8 && scatteredSource.Entries > 1)
                        fieldElement.Add(new XAttribute("stride", stride));

                    scatteredElement.Add(fieldElement);
                }

                element.Add(scatteredElement);
            }
            else
            {
                throw new NotSupportedException($"Color source model of type '{source.GetType()}' is not supported");
            }
        }

        return element;
    }

    internal override MagitekResults Build(ProjectTreeBuilder builder, ResourceModel model, string parentNodePath, string fileLocation) =>
        MagitekResults.From(builder.AddPalette((PaletteModel)model, parentNodePath, fileLocation));

    public override ResourceModel WithKeys(ResourceModel model, string name, Func<string, string> map)
    {
        var x = (PaletteModel)model;
        return x with { Name = name, DataFileKey = x.DataFileKey is null ? null : map(x.DataFileKey) };
    }

    internal override string Summarize(ResourceModel model) =>
        $"{((PaletteModel)model).ColorSources.Sum(x => x switch { FileColorSourceModel file => file.Entries, ScatteredColorSourceModel scattered => scattered.Entries, _ => 1 })} colors";

    internal override IEnumerable<long> Offsets(ResourceModel model) =>
        ((PaletteModel)model).ColorSources.OfType<FileColorSourceModel>().Select(x => x.FileAddress);
}
