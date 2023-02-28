using System;
using System.Xml;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Xml.Schema;
using System.Collections.Generic;
using ImageMagitek.ExtensionMethods;
using CommunityToolkit.Diagnostics;

namespace ImageMagitek.Codec;

public sealed class XmlGraphicsFormatReader : IGraphicsFormatReader
{
    private static class Names
    {
        public const string Name = "name";
        public const string ColorType = "colortype";
        public const string Layout = "layout";
        public const string DefaultWidth = "defaultwidth";
        public const string DefaultHeight = "defaultheight";
        public const string FixedSize = "fixedsize";
        public const string MergePriority = "mergepriority";
        public const string Images = "images";
        public const string Indexed = "indexed";
        public const string Direct = "direct";
        public const string Tiled = "tiled";
        public const string Single = "single";
        public const string True = "true";
        public const string False = "false";

        public const string FlowCodec = "flowcodec";
        public const string PatternCodec = "patterncodec";

        public const string Image = "image";
        public const string ColorDepth = "colordepth";
        public const string RowInterlace = "rowinterlace";
        public const string RowPixelPattern = "rowpixelpattern";

        public const string Packing = "packing";
        public const string Width = "width";
        public const string Height = "height";
        public const string Pattern = "pattern";
        public const string Patterns = "patterns";
        public const string Planar = "planar";
        public const string Chunky = "chunky";
        public const string Size = "size";
    }

    private readonly XmlSchemaSet _schemas = new();

    public XmlGraphicsFormatReader(string schemaFileName)
    {
        Guard.IsNotNullOrWhiteSpace(schemaFileName);

        if (!File.Exists(schemaFileName))
        {
            throw new ArgumentException($"{nameof(schemaFileName)} cannot be found");
        }
        else
        {
            using var schemaStream = File.OpenRead(schemaFileName);
            _schemas.Add("", XmlReader.Create(schemaStream));
        }
    }

    public MagitekResults<IGraphicsFormat> LoadFromFile(string fileName)
    {
        if (!File.Exists(fileName))
            return new MagitekResults<IGraphicsFormat>.Failed($"Codec file {fileName} does not exist");

        using var stream = File.OpenRead(fileName);
        var doc = XDocument.Load(stream, LoadOptions.SetLineInfo);

        var validationErrors = new List<string>();

        doc.Validate(_schemas, (o, e) =>
        {
            validationErrors.Add(e.Message);
        });

        if (validationErrors.Any())
        {
            validationErrors.Insert(0, $"Codec '{fileName}' failed to be validated");
            return new MagitekResults<IGraphicsFormat>.Failed(validationErrors);
        }

        return doc.Root?.Name.LocalName switch
        {
            Names.FlowCodec => ReadFlowCodec(doc.Root),
            Names.PatternCodec => ReadPatternCodec(doc.Root),
            null => new MagitekResults<IGraphicsFormat>.Failed($"No root element"),
            _ => new MagitekResults<IGraphicsFormat>.Failed($"Unrecognized codec root element '{doc.Root.Name}'")
        };
    }

    private static MagitekResults<IGraphicsFormat> ReadFlowCodec(XElement flowElementRoot)
    {
        var errors = new List<string>();

        var name = flowElementRoot.Attribute(Names.Name)?.Value;

        if (name is null)
            AddMissingError(errors, flowElementRoot, Names.Name);

        name ??= "Missing";

        var codec = new
        {
            ColorType = flowElementRoot.Element(Names.ColorType),
            ColorDepth = flowElementRoot.Element(Names.ColorDepth),
            Layout = flowElementRoot.Element(Names.Layout),
            DefaultWidth = flowElementRoot.Element(Names.DefaultWidth),
            DefaultHeight = flowElementRoot.Element(Names.DefaultHeight),
            FixedSize = flowElementRoot.Element(Names.FixedSize),
            MergePriority = flowElementRoot.Element(Names.MergePriority),
            Images = flowElementRoot.Element(Names.Images)
        };

        if (!int.TryParse(codec.DefaultWidth?.Value, out var defaultWidth))
            AddParseError(errors, codec.DefaultWidth, Names.DefaultWidth, flowElementRoot);

        if (defaultWidth <= 1)
            AddValidationError(errors, codec.DefaultWidth, $"Specified {Names.DefaultWidth} is too small.");

        if (!int.TryParse(codec.DefaultHeight?.Value, out var defaultHeight))
            AddParseError(errors, codec.DefaultHeight, Names.DefaultHeight, flowElementRoot);

        if (defaultHeight <= 1)
            AddValidationError(errors, codec.DefaultHeight, $"Specified {Names.DefaultHeight} is too small.");

        PixelColorType colorType = default;
        if (codec.ColorType?.Value == Names.Indexed)
            colorType = PixelColorType.Indexed;
        else if (codec.ColorType?.Value == Names.Direct)
            colorType = PixelColorType.Direct;
        else if (codec.ColorType is not null)
            AddParseError(errors, codec.ColorType, Names.ColorType, flowElementRoot);
        else
            AddMissingError(errors, flowElementRoot, Names.ColorType);

        if (!int.TryParse(codec.ColorDepth?.Value, out var colorDepth))
            AddParseError(errors, codec.ColorDepth, Names.ColorDepth, flowElementRoot);

        if (colorDepth < 1 && colorDepth > 32)
            AddValidationError(errors, codec.ColorDepth, $"{Names.ColorDepth} is out of range: '{colorDepth}'.");

        ImageLayout layout = default;
        if (codec.Layout?.Value == Names.Tiled)
            layout = ImageLayout.Tiled;
        else if (codec.Layout?.Value == Names.Single)
            layout = ImageLayout.Single;
        else
            AddParseError(errors, codec.Layout, Names.Layout, flowElementRoot);

        bool fixedSize = false;
        if (codec.FixedSize?.Value == Names.True)
            fixedSize = true;
        else if (codec.FixedSize?.Value == Names.False)
            fixedSize = false;
        else
            AddParseError(errors, codec.FixedSize, Names.FixedSize, flowElementRoot);

        var format = new FlowGraphicsFormat(name ?? "Missing", colorType, colorDepth, layout, defaultWidth, defaultHeight)
        {
            FixedSize = fixedSize
        };

        string mergeString = codec.MergePriority?.Value ?? "";
        mergeString = mergeString.Replace(" ", "");
        var mergeItems = mergeString.Split(',');

        if (mergeItems.Length == format.ColorDepth)
        {
            format.MergePlanePriority = new int[format.ColorDepth];

            for (int i = 0; i < mergeItems.Length; i++)
            {
                if (int.TryParse(mergeItems[i], out var mergePlane))
                    format.MergePlanePriority[i] = mergePlane;
                else
                    AddParseError(errors, codec.MergePriority, Names.MergePriority, flowElementRoot);
            }
        }
        else
            AddValidationError(errors, codec.MergePriority, $"The number of entries in {Names.MergePriority} do not match the {Names.ColorDepth}.");

        var imageElement = flowElementRoot.Element(Names.Images);
        var imageElements = imageElement?.Elements(Names.Image) ?? Enumerable.Empty<XElement>();
        var images = imageElements.Select(x => new
        {
            ColorDepth = x.Element(Names.ColorDepth),
            RowInterlace = x.Element(Names.RowInterlace),
            RowPixelPattern = x.Element(Names.RowPixelPattern)
        });

        foreach (var image in images)
        {
            int[] rowPixelPattern;

            if (image.RowPixelPattern is not null) // Parse rowpixelpattern
            {
                string patternString = image.RowPixelPattern.Value;
                patternString = patternString.Replace(" ", "");
                var patternInputs = patternString.Split(',');

                rowPixelPattern = new int[patternInputs.Length];

                for (int i = 0; i < patternInputs.Length; i++)
                {
                    if (int.TryParse(patternInputs[i], out var patternPriority))
                        rowPixelPattern[i] = patternPriority;
                    else
                        AddParseError(errors, image.RowPixelPattern, Names.RowPixelPattern, imageElement!);
                }
            }
            else // Create a default rowpixelpattern
            {
                rowPixelPattern = new int[1] { 0 };
            }

            if (!int.TryParse(image.ColorDepth?.Value, out var imageColorDepth))
                AddParseError(errors, image.ColorDepth, Names.ColorDepth, imageElement!);

            if (!bool.TryParse(image.RowInterlace?.Value, out var imageRowInterlace))
                AddParseError(errors, image.RowInterlace, Names.RowInterlace, imageElement!);

            var ip = new ImageProperty(imageColorDepth, imageRowInterlace, rowPixelPattern);
            format.ImageProperties.Add(ip);
        }

        var sumColorDepth = format.ImageProperties.Sum(x => x.ColorDepth);
        if (format.ColorDepth != sumColorDepth)
        {
            errors.Add($"Codec's colordepth '{format.ColorDepth}' does not match the sum of all image colordepth elements '{sumColorDepth}'");
        }

        if (errors.Any())
            return new MagitekResults<IGraphicsFormat>.Failed(errors);
        else
            return new MagitekResults<IGraphicsFormat>.Success(format);
    }

    private static MagitekResults<IGraphicsFormat> ReadPatternCodec(XElement patternElementRoot)
    {
        var errors = new List<string>();

        var name = patternElementRoot.Attribute(Names.Name)?.Value;

        if (name is null)
            AddMissingError(errors, patternElementRoot, Names.Name);

        name ??= "Missing";

        var codec = new
        {
            ColorType = patternElementRoot.Element(Names.ColorType),
            ColorDepth = patternElementRoot.Element(Names.ColorDepth),
            Layout = patternElementRoot.Element(Names.Layout),
            Packing = patternElementRoot.Element(Names.Packing),
            MergePriority = patternElementRoot.Element(Names.MergePriority),
            RowPixelPattern = patternElementRoot.Element(Names.RowPixelPattern),
            Width = patternElementRoot.Element(Names.Width),
            Height = patternElementRoot.Element(Names.Height),
            Patterns = patternElementRoot.Element(Names.Patterns)
        };

        if (!int.TryParse(codec.Width?.Value, out var width))
            AddParseError(errors, codec.Width, Names.Width, patternElementRoot);

        if (width <= 1)
            AddValidationError(errors, codec.Width, $"Specified '{Names.Width}' is too small.");

        if (!int.TryParse(codec.Height?.Value, out var height))
            AddParseError(errors, codec.Height, Names.Height, patternElementRoot);

        if (height <= 1)
            AddValidationError(errors, codec.Height, $"Specified '{Names.Height}' is too small.");

        PixelColorType colorType = default;
        if (codec.ColorType?.Value == Names.Indexed)
            colorType = PixelColorType.Indexed;
        else if (codec.ColorType?.Value == Names.Direct)
            colorType = PixelColorType.Direct;
        else if (codec.ColorType is not null)
            AddParseError(errors, codec.ColorType, Names.ColorType, patternElementRoot);
        else
            AddMissingError(errors, patternElementRoot, Names.ColorType);

        if (!int.TryParse(codec.ColorDepth?.Value, out var colorDepth))
            AddParseError(errors, codec.ColorDepth, Names.ColorDepth, patternElementRoot);

        if (colorDepth < 1 || colorDepth > 32)
            AddValidationError(errors, codec.ColorDepth, $"{Names.ColorDepth} is out of range: '{colorDepth}'.");

        ImageLayout layout = default;
        if (codec.Layout?.Value == Names.Tiled)
            layout = ImageLayout.Tiled;
        else if (codec.Layout?.Value == Names.Single)
            layout = ImageLayout.Single;
        else
            AddParseError(errors, codec.Layout, Names.Layout, patternElementRoot);

        PixelPacking packing = default;
        if (codec.Packing?.Value == Names.Planar)
            packing = PixelPacking.Planar;
        else if (codec.Packing?.Value == Names.Chunky)
            packing = PixelPacking.Chunky;
        else
            AddParseError(errors, codec.Packing, Names.Packing, patternElementRoot);

        int[] rowPixelPattern;
        var rowPixelPatternString = codec.RowPixelPattern?.Value;
        if (rowPixelPatternString is not null)
        {
            rowPixelPatternString = rowPixelPatternString.Replace(" ", "");
            var patternInputs = rowPixelPatternString.Split(',');

            rowPixelPattern = new int[patternInputs.Length];

            for (int i = 0; i < patternInputs.Length; i++)
            {
                if (int.TryParse(patternInputs[i], out var patternPriority))
                    rowPixelPattern[i] = patternPriority;
                else
                    AddParseError(errors, codec.RowPixelPattern, Names.RowPixelPattern, patternElementRoot);
            }
        }
        else // Create a default rowpixelpattern
        {
            rowPixelPattern = new int[1];
        }

        var rowPixelPatternList = new RepeatList(rowPixelPattern);

        string mergeString = codec.MergePriority?.Value ?? "";
        mergeString = mergeString.Replace(" ", "");
        var mergeItems = mergeString.Split(',');


        int[]? mergePlanePriority = default;
        if (mergeItems.Length == colorDepth)
        {
            mergePlanePriority = new int[colorDepth];

            for (int i = 0; i < mergeItems.Length; i++)
            {
                if (int.TryParse(mergeItems[i], out var mergePlane))
                    mergePlanePriority[i] = mergePlane;
                else
                    AddParseError(errors, codec.MergePriority, Names.MergePriority, patternElementRoot);
            }
        }
        else
            AddValidationError(errors, codec.MergePriority, $"The number of entries in {Names.MergePriority} does not match the {Names.ColorDepth}");

        var patternsElement = patternElementRoot.Element(Names.Patterns);
        var sizeElement = patternsElement?.Attribute(Names.Size);

        if (!int.TryParse(sizeElement?.Value, out var patternSize))
            AddParseError(errors, sizeElement, Names.Size, patternsElement!);

        if (patternSize < 1 || patternSize > PatternList.MaxPatternSize)
            AddValidationError(errors, sizeElement, $"{Names.Size} is out of range");

        var patternStrings = patternsElement
            ?.Elements(Names.Pattern)
            .Select(x => string.Join("", x.Value.Where(c => !char.IsWhiteSpace(c))))
            .ToArray();

        PatternList? patternList = default;

        if (patternStrings is not null)
        {
            var patternResult = PatternList.TryCreatePatternList(patternStrings, packing, width, height, colorDepth, patternSize);

            patternResult.Switch(
                success => patternList = success.Result,
                failed => errors.Add(failed.Reason));
        }
        else
        {
            AddParseError(errors, patternsElement, Names.Patterns, patternElementRoot);
        }

        if (errors.Any())
        {
            return new MagitekResults<IGraphicsFormat>.Failed(errors);
        }
        else
        {
            var format = new PatternGraphicsFormat(name, colorType, colorDepth, layout, packing, width, height, mergePlanePriority!, rowPixelPatternList, patternList!); ;
            return new MagitekResults<IGraphicsFormat>.Success(format);
        }
    }

    private static void AddParseError(List<string> errors, XObject? errorElement, string errorElementName, XElement parentElement)
    {
        if (errorElement?.LineNumber() is int line)
        {
            errors.Add($"Element '{errorElementName}' could not be parsed. Line {line}");
        }
        else
        {
            AddMissingError(errors, parentElement, errorElementName);
        }
    }

    private static void AddValidationError(List<string> errors, XObject? errorElement, string reason)
    {
        var line = errorElement?.LineNumber();

        if (line is not null)
        {
            errors.Add($"{reason} Line {line}");
        }
        else
        {
            errors.Add($"{reason}");
        }
    }

    private static void AddMissingError(List<string> errors, XElement parentElement, string missingElementName)
    {
        var errorMessage = $"Element '{parentElement.Name}' is missing '{missingElementName}' on Line '{parentElement.LineNumber()}'";
        errors.Add(errorMessage);
    }
}
