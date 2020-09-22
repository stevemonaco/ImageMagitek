using System;
using System.Xml;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Xml.Schema;
using System.Collections.Generic;

namespace ImageMagitek.Codec
{
    public class XmlGraphicsFormatReader : IGraphicsFormatReader
    {
        private XmlSchemaSet _schemas = new XmlSchemaSet();

        public XmlGraphicsFormatReader(string schemaFileName)
        {
            if (!string.IsNullOrWhiteSpace(schemaFileName))
            {
                using var schemaStream = File.OpenRead(schemaFileName);
                _schemas.Add("", XmlReader.Create(schemaStream));
            }
        }

        public MagitekResults<GraphicsFormat> LoadFromFile(string fileName)
        {
            var format = new GraphicsFormat();

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
                return new MagitekResults<GraphicsFormat>.Failed(validationErrors);
            }


            XElement formatNode = doc.Element("format");

            format.Name = formatNode.Attribute("name").Value;

            var codecs = formatNode.Descendants("codec")
                .Select(e => new
                {
                    colortype = e.Descendants("colortype").First().Value,
                    colordepth = e.Descendants("colordepth").First().Value,
                    layout = e.Descendants("layout").First().Value,
                    height = e.Descendants("defaultheight").First().Value,
                    width = e.Descendants("defaultwidth").First().Value,
                    fixedsize = e.Descendants("fixedsize").First().Value,
                    mergepriority = e.Descendants("mergepriority").First().Value
                }).First();

            if (codecs.colortype == "indexed")
                format.ColorType = PixelColorType.Indexed;
            else if (codecs.colortype == "direct")
                format.ColorType = PixelColorType.Direct;
            else
                throw new XmlException($"Unsupported colortype '{codecs.colortype}' while parsing codec '{fileName}'");

            format.ColorDepth = int.Parse(codecs.colordepth);

            if (codecs.layout == "tiled")
                format.Layout = ImageLayout.Tiled;
            else if (codecs.layout == "linear")
                format.Layout = ImageLayout.Single;
            else
                throw new XmlException($"Unsupported layout '{codecs.layout}' while parsing codec '{fileName}'");

            if (codecs.fixedsize == "true")
                format.FixedSize = true;

            format.DefaultWidth = int.Parse(codecs.width);
            format.DefaultHeight = int.Parse(codecs.height);
            format.Width = format.DefaultWidth;
            format.Height = format.DefaultHeight;
            format.FixedSize = bool.Parse(codecs.fixedsize);

            string mergestring = codecs.mergepriority;
            mergestring.Replace(" ", "");
            string[] mergeInts = mergestring.Split(',');

            if (mergeInts.Length != format.ColorDepth)
                throw new Exception("The number of entries in mergepriority does not match the colordepth while parsing codec '{fileName}'");

            format.MergePlanePriority = new int[format.ColorDepth];

            for (int i = 0; i < mergeInts.Length; i++)
                format.MergePlanePriority[i] = int.Parse(mergeInts[i]);

            var images = formatNode.Descendants("image")
                         .Select(e => new
                         {
                             colordepth = e.Descendants("colordepth").First().Value,
                             rowinterlace = e.Descendants("rowinterlace").First().Value,
                             rowpixelpattern = e.Descendants("rowpixelpattern")
                         });

            foreach (var image in images)
            {
                int[] rowPixelPattern;

                if (image.rowpixelpattern.Any()) // Parse rowpixelpattern
                {
                    string order = image.rowpixelpattern.First().Value;
                    order = order.Replace(" ", "");
                    string[] orderInts = order.Split(',');

                    rowPixelPattern = new int[orderInts.Length];

                    for (int i = 0; i < orderInts.Length; i++)
                        rowPixelPattern[i] = int.Parse(orderInts[i]);
                }
                else // Create a default rowpixelpattern
                {
                    rowPixelPattern = new int[1];
                }

                ImageProperty ip = new ImageProperty(int.Parse(image.colordepth), bool.Parse(image.rowinterlace), rowPixelPattern);
                format.ImageProperties.Add(ip);
            }

            return new MagitekResults<GraphicsFormat>.Success(format);
        }
    }
}
