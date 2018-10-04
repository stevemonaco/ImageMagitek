using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using System.Xml.Linq;

namespace ImageMagitek.Codec
{
    public class XmlGraphicsFormatSerializer : IGraphicsFormatSerializer
    {
        public GraphicsFormat LoadFromFile(string fileName)
        {
            var format = new GraphicsFormat();

            XElement xe = XElement.Load(fileName);

            format.Name = xe.Attribute("name").Value;

            var codecs = xe.Descendants("codec")
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
                throw new XmlException(String.Format("Unsupported colortype '{0}'", codecs.colortype));

            format.ColorDepth = int.Parse(codecs.colordepth);

            if (codecs.layout == "tiled")
                format.Layout = ImageLayout.Tiled;
            else if (codecs.layout == "linear")
                format.Layout = ImageLayout.Linear;
            else
                throw new XmlException(String.Format("Unsupported layout '{0}'", codecs.layout));

            format.DefaultWidth = int.Parse(codecs.width);
            format.DefaultHeight = int.Parse(codecs.height);
            format.Width = format.DefaultWidth;
            format.Height = format.DefaultHeight;
            format.FixedSize = bool.Parse(codecs.fixedsize);

            string mergestring = codecs.mergepriority;
            mergestring.Replace(" ", "");
            string[] mergeInts = mergestring.Split(',');

            if (mergeInts.Length != format.ColorDepth)
                throw new Exception("The number of entries in mergepriority does not match the colordepth");

            format.MergePriority = new int[format.ColorDepth];

            for (int i = 0; i < mergeInts.Length; i++)
                format.MergePriority[i] = int.Parse(mergeInts[i]);

            var images = xe.Descendants("image")
                         .Select(e => new
                         {
                             colordepth = e.Descendants("colordepth").First().Value,
                             rowinterlace = e.Descendants("rowinterlace").First().Value,
                             rowpixelpattern = e.Descendants("rowpixelpattern")
                         });

            foreach (var image in images)
            {
                int[] rowPixelPattern;

                if (image.rowpixelpattern.Count() > 0) // Parse rowpixelpattern
                {
                    string order = image.rowpixelpattern.First().Value;
                    order.Replace(" ", "");
                    string[] orderInts = order.Split(',');

                    rowPixelPattern = new int[orderInts.Length];

                    for (int i = 0; i < orderInts.Length; i++)
                        rowPixelPattern[i] = int.Parse(orderInts[i]);
                }
                else // Create a default rowpixelpattern in numeric order for the entire row
                {
                    rowPixelPattern = new int[format.Width];

                    for (int i = 0; i < format.Width; i++)
                        rowPixelPattern[i] = i;
                }

                ImageProperty ip = new ImageProperty(int.Parse(image.colordepth), bool.Parse(image.rowinterlace), rowPixelPattern);
                ip.ExtendRowPattern(format.Width);
                format.ImageProperties.Add(ip);
            }

            return format;
        }
    }
}
