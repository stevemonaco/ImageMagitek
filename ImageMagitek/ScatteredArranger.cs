using System;
using System.Drawing;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using ImageMagitek.ExtensionMethods;
using ImageMagitek.Project;

namespace ImageMagitek
{
    public class ScatteredArranger: Arranger
    {
        public ScatteredArranger()
        {
            Mode = ArrangerMode.ScatteredArranger;
        }

        /// <summary>
        /// Creates a new scattered arranger with default initialized elements
        /// </summary>
        /// <param name="layout">Layout type of the arranger</param>
        /// <param name="arrangerWidth">Width of Arranger in Elements</param>
        /// <param name="arrangerHeight">Height of Arranger in Elements</param>
        /// <param name="elementWidth">Width of each element</param>
        /// <param name="elementHeight">Height of each element</param>
        /// <returns></returns>
        public ScatteredArranger(ArrangerLayout layout, int arrangerWidth, int arrangerHeight, int elementWidth, int elementHeight)
        {
            Mode = ArrangerMode.ScatteredArranger;
            Layout = layout;
            ElementGrid = new ArrangerElement[arrangerWidth, arrangerHeight];

            int x = 0;
            int y = 0;

            for (int i = 0; i < arrangerHeight; i++)
            {
                x = 0;
                for (int j = 0; j < arrangerWidth; j++)
                {
                    ArrangerElement el = new ArrangerElement()
                    {
                        Parent = this,
                        X1 = x,
                        Y1 = y,
                        X2 = x + elementWidth - 1,
                        Y2 = y + elementHeight - 1,
                        Width = elementWidth,
                        Height = elementHeight,
                    };
                    ElementGrid[j, i] = el;

                    x += elementWidth;
                }
                y += elementHeight;
            }

            ArrangerElement LastElem = ElementGrid[arrangerWidth - 1, arrangerHeight - 1];
            ArrangerPixelSize = new Size(LastElem.X2 + 1, LastElem.Y2 + 1);
            ArrangerElementSize = new Size(arrangerWidth, arrangerHeight);
            ElementPixelSize = new Size(elementWidth, elementHeight);
        }

        /// <summary>
        /// Resizes a scattered arranger to the specified number of elements and default initializes any new elements
        /// </summary>
        /// <param name="arrangerWidth">Width of Arranger in Elements</param>
        /// <param name="arrangerHeight">Height of Arranger in Elements</param>
        public override void Resize(int arrangerWidth, int arrangerHeight)
        {
            if (Mode != ArrangerMode.ScatteredArranger)
                throw new ArgumentException();

            ArrangerElement[,] newList = new ArrangerElement[arrangerWidth, arrangerHeight];

            int xCopy = Math.Min(arrangerWidth, ArrangerElementSize.Width);
            int yCopy = Math.Min(arrangerHeight, ArrangerElementSize.Height);
            int Width = ElementPixelSize.Width;
            int Height = ElementPixelSize.Height;

            for (int y = 0; y < arrangerHeight; y++)
            {
                for (int x = 0; x < arrangerWidth; x++)
                {
                    if ((y < ArrangerElementSize.Height) && (x < ArrangerElementSize.Width)) // Copy from old arranger
                        newList[x, y] = ElementGrid[x, y].Clone();
                    else // Create new blank element
                    {
                        ArrangerElement el = new ArrangerElement()
                        {
                            Parent = this,
                            X1 = x * Width,
                            Y1 = y * Height,
                            X2 = x * Width + Width - 1,
                            Y2 = y * Height + Height - 1,
                            Width = Width,
                            Height = Height,
                        };

                        newList[x, y] = el;
                    }
                }
            }

            ElementGrid = newList;
            ArrangerElementSize = new Size(arrangerWidth, arrangerHeight);
            ArrangerPixelSize = new Size(arrangerWidth * Width, arrangerHeight * Height);
        }

        #region ProjectResource implementation
        public override ProjectResourceBase Clone()
        {
            Arranger arr = new ScatteredArranger()
            {
                ElementGrid = new ArrangerElement[ArrangerElementSize.Width, ArrangerElementSize.Height],
                ArrangerElementSize = ArrangerElementSize,
                ElementPixelSize = ElementPixelSize,
                ArrangerPixelSize = ArrangerPixelSize,
                Mode = Mode,
                Name = Name,
            };

            for (int y = 0; y < ArrangerElementSize.Height; y++)
                for (int x = 0; x < ArrangerElementSize.Width; x++)
                    arr.SetElement(ElementGrid[x, y], x, y);

            return arr;
        }

        public override XElement Serialize()
        {
            XElement xe = new XElement("arranger");

            xe.SetAttributeValue("name", Name);
            xe.SetAttributeValue("elementsx", ArrangerElementSize.Width);
            xe.SetAttributeValue("elementsy", ArrangerElementSize.Height);
            xe.SetAttributeValue("width", ElementPixelSize.Width);
            xe.SetAttributeValue("height", ElementPixelSize.Height);

            if (Layout == ArrangerLayout.TiledArranger)
                xe.SetAttributeValue("layout", "tiled");
            else if (Layout == ArrangerLayout.LinearArranger)
                xe.SetAttributeValue("layout", "linear");

            string DefaultPalette = this.FindMostFrequentElementValue("PaletteKey");
            string DefaultFile = this.FindMostFrequentElementValue("DataFileKey");
            string DefaultFormat = this.FindMostFrequentElementValue("FormatName");

            xe.SetAttributeValue("defaultformat", DefaultFormat);
            xe.SetAttributeValue("defaultdatafile", DefaultFile);
            xe.SetAttributeValue("defaultpalette", DefaultPalette);

            foreach(var el in EnumerateElements())
            {
                var graphic = new XElement("element");

                graphic.SetAttributeValue("fileoffset", String.Format("{0:X}", el.FileAddress.FileOffset));
                if (el.FileAddress.BitOffset != 0)
                    graphic.SetAttributeValue("bitoffset", String.Format("{0:X}", el.FileAddress.BitOffset));
                graphic.SetAttributeValue("posx", el.X1 / el.Width);
                graphic.SetAttributeValue("posy", el.Y1 / el.Height);
                if (el.FormatName != DefaultFormat)
                    graphic.SetAttributeValue("format", el.FormatName);
                if (el.DataFileKey != DefaultFile)
                    graphic.SetAttributeValue("file", el.DataFileKey);
                if (el.PaletteKey != DefaultPalette)
                    graphic.SetAttributeValue("palette", el.PaletteKey);

                xe.Add(graphic);
            }

            /*for (int y = 0; y < ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < ArrangerElementSize.Width; x++)
                {
                    var graphic = new XElement("element");
                    ArrangerElement el = GetElement(x, y);

                    graphic.SetAttributeValue("fileoffset", String.Format("{0:X}", el.FileAddress.FileOffset));
                    if (el.FileAddress.BitOffset != 0)
                        graphic.SetAttributeValue("bitoffset", String.Format("{0:X}", el.FileAddress.BitOffset));
                    graphic.SetAttributeValue("posx", x);
                    graphic.SetAttributeValue("posy", y);
                    if (el.FormatName != DefaultFormat)
                        graphic.SetAttributeValue("format", el.FormatName);
                    if (el.DataFileKey != DefaultFile)
                        graphic.SetAttributeValue("file", el.DataFileKey);
                    if (el.PaletteKey != DefaultPalette)
                        graphic.SetAttributeValue("palette", el.PaletteKey);

                    xe.Add(graphic);
                }
            }*/

            return xe;
        }

        public override bool Deserialize(XElement element)
        {
            string name = element.Attribute("name").Value;
            int elementsx = int.Parse(element.Attribute("elementsx").Value); // Width of arranger in elements
            int elementsy = int.Parse(element.Attribute("elementsy").Value); // Height of arranger in elements
            int width = int.Parse(element.Attribute("width").Value); // Width of element in pixels
            int height = int.Parse(element.Attribute("height").Value); // Height of element in pixels
            string defaultFormat = element.Attribute("defaultformat").Value;
            string defaultDataFile = element.Attribute("defaultdatafile").Value;
            string defaultPalette = element.Attribute("defaultpalette").Value;
            string layoutName = element.Attribute("layout").Value;
            IEnumerable<XElement> elementList = element.Descendants("element");

            if (layoutName == "tiled")
                Layout = ArrangerLayout.TiledArranger;
            else if (layoutName == "linear")
                Layout = ArrangerLayout.LinearArranger;
            else
                throw new XmlException("Incorrect arranger layout type ('" + layoutName + "') for " + name);

            Name = name;
            ElementPixelSize = new Size(width, height);
            Resize(elementsx, elementsy);

            var xmlElements = elementList.Select(e => new
            {
                fileoffset = long.Parse(e.Attribute("fileoffset").Value, System.Globalization.NumberStyles.HexNumber),
                bitoffset = e.Attribute("bitoffset"),
                posx = int.Parse(e.Attribute("posx").Value),
                posy = int.Parse(e.Attribute("posy").Value),
                format = e.Attribute("format"),
                palette = e.Attribute("palette"),
                file = e.Attribute("file")
            });

            foreach (var xmlElement in xmlElements)
            {
                ArrangerElement el = GetElement(xmlElement.posx, xmlElement.posy);

                el.DataFileKey = xmlElement.file?.Value ?? defaultDataFile;
                el.PaletteKey = xmlElement.palette?.Value ?? defaultPalette;
                el.FormatName = xmlElement.format?.Value ?? defaultFormat;

                if (xmlElement.bitoffset != null)
                    el.FileAddress = new FileBitAddress(xmlElement.fileoffset, int.Parse(xmlElement.bitoffset.Value));
                else
                    el.FileAddress = new FileBitAddress(xmlElement.fileoffset, 0);

                el.Height = height;
                el.Width = width;
                el.X1 = xmlElement.posx * el.Width;
                el.Y1 = xmlElement.posy * el.Height;
                el.X2 = el.X1 + el.Width - 1;
                el.Y2 = el.Y1 + el.Height - 1;

                SetElement(el, xmlElement.posx, xmlElement.posy);
            }

            return true;
        }
        #endregion
    }
}
