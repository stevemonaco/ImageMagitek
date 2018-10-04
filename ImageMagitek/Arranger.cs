using System;
using System.Drawing;
using System.Xml.Linq;
using ImageMagitek.Project;

namespace ImageMagitek
{
    /// <summary>
    /// Mode for the arranger.
    /// Sequential arrangers are for sequential file access
    /// Scattered arrangers are for accessing many files and file offsets in a single arranger
    /// Memory arrangers are used as a scratchpad (currently unimplemented)
    /// </summary>
    public enum ArrangerMode { SequentialArranger = 0, ScatteredArranger, MemoryArranger };

    /// <summary>
    /// Layout of graphics for the arranger
    /// Each layout directs the UI to perform differently
    /// TiledArranger will snap selection rectangles to tile boundaries
    /// LinearArranger will snap selection rectangles to pixel boundaries
    /// </summary>
    public enum ArrangerLayout { TiledArranger = 0, LinearArranger };

    /// <summary>
    /// Move operations for sequential arrangers
    /// </summary>
    public enum ArrangerMoveType { ByteDown = 0, ByteUp, RowDown, RowUp, ColRight, ColLeft, PageDown, PageUp, Home, End, Absolute };

    /// <summary>
    /// Arranger for graphical screen elements
    /// </summary>
    public abstract class Arranger : ProjectResourceBase
    {
        // General Arranger variables
        /// <summary>
        /// Gets individual Elements that compose the Arranger
        /// </summary>
        public ArrangerElement[,] ElementGrid { get; set; }

        /// <summary>
        /// Gets the size of the entire Arranger in Elements
        /// </summary>
        public Size ArrangerElementSize { get; protected set; }

        /// <summary>
        /// Gets the Size of the entire Arranger in unzoomed pixels
        /// </summary>
        public Size ArrangerPixelSize { get; protected set; }

        /// <summary>
        /// Gets the size of an individual Element in unzoomed pixels
        /// Only valid for Sequential Arranger
        /// </summary>
        public Size ElementPixelSize { get; protected set; }

        /// <summary>
        /// Gets the Mode of the Arranger
        /// </summary>
        public ArrangerMode Mode { get; protected set; }

        /// <summary>
        /// Gets the ArrangerLayout of the Arranger
        /// </summary>
        public ArrangerLayout Layout { get; protected set; }

        /// <summary>
        /// Renames an Arranger to a new name
        /// </summary>
        /// <param name="name"></param>
        public override void Rename(string name)
        {
            Name = name;
        }

        public abstract void Resize(int arrangerWidth, int arrangerHeight);

        /*void NewBlankArranger(int ElementsX, int ElementsY, GraphicsFormat format)
        {
            if (format == null)
                throw new ArgumentNullException();

            ElementList = new ArrangerElement[ElementsX, ElementsY];

            int x = 0;
            int y = 0;

            for (int i = 0; i < ElementsY; i++)
            {
                x = 0;
                for (int j = 0; j < ElementsX; j++)
                {
                    ArrangerElement el = new ArrangerElement();
                    // Filename
                    el.FileOffset = 0;
                    el.X1 = x;
                    el.Y1 = y;
                    el.X2 = x + format.Width - 1;
                    el.Y2 = y + format.Height - 1;
                    el.Format = format.Name;
                    el.Palette = "Default";
                    ElementList[j, i] = el;

                    x += format.Width;
                }
                y += format.Height;
            }

            ArrangerElement LastElem = ElementList[ElementsX - 1, ElementsY - 1];
            ArrangerPixelSize = new Size(LastElem.X2, LastElem.Y2);
        }*/

        /// <summary>
        /// Creates a new Scattered Arranger from an existing Arranger
        /// </summary>
        /// <param name="subArrangerName">Arranger name for the newly created Arranger</param>
        /// <param name="arrangerPosX">0-based top-most Element coordinate of parent Arranger selection to copy</param>
        /// <param name="arrangerPosY">0-based left-most Element coordinate of parent Arranger selection to copy</param>
        /// <param name="copyWidth">Width of selection to copy in Elements</param>
        /// <param name="copyHeight">Height of selection to copy in Elements</param>
        /// <returns></returns>
        public Arranger CreateSubArranger(string subArrangerName, int arrangerPosX, int arrangerPosY, int copyWidth, int copyHeight)
        {
            if ((arrangerPosX < 0) || (arrangerPosX + copyWidth > ArrangerElementSize.Width))
                throw new ArgumentOutOfRangeException();

            if ((arrangerPosY < 0) || (arrangerPosY + copyHeight > ArrangerElementSize.Height))
                throw new ArgumentOutOfRangeException();
            Arranger sub = new ScatteredArranger(Layout, ArrangerElementSize.Width, ArrangerElementSize.Height, ElementPixelSize.Width, ElementPixelSize.Height);

            Arranger subArranger = new ScatteredArranger()
            {
                Mode = ArrangerMode.ScatteredArranger, // Default to scattered arranger
                Name = subArrangerName,
                Layout = Layout,
                ElementGrid = new ArrangerElement[copyWidth, copyHeight],
                ArrangerElementSize = new Size(copyWidth, copyHeight),
                ElementPixelSize = ElementPixelSize,
                ArrangerPixelSize = new Size(ElementPixelSize.Width * copyWidth, ElementPixelSize.Height * copyHeight)
            };

            for (int srcy = arrangerPosY, desty = 0; srcy < arrangerPosY + copyHeight; srcy++, desty++)
            {
                for (int srcx = arrangerPosX, destx = 0; srcx < arrangerPosX + copyWidth; srcx++, destx++)
                {
                    ArrangerElement el = GetElement(srcx, srcy).Clone();
                    el.X1 = destx * subArranger.ElementPixelSize.Width;
                    el.X2 = el.X1 + subArranger.ElementPixelSize.Width - 1;
                    el.Y1 = desty * subArranger.ElementPixelSize.Height;
                    el.Y2 = el.Y1 + subArranger.ElementPixelSize.Height - 1;
                    subArranger.SetElement(el, destx, desty);
                }
            }

            return subArranger;
        }

        /// <summary>
        /// Sets Element to a position in the Arranger ElementGrid using a shallow copy
        /// </summary>
        /// <param name="element">Element to be placed into the ElementGrid</param>
        /// <param name="arrangerPosX">0-based x-coordinate of Element</param>
        /// <param name="arrangerPosY">0-based y-coordinate of Element</param>
        public void SetElement(ArrangerElement element, int arrangerPosX, int arrangerPosY)
        {
            if (ElementGrid is null)
                throw new ArgumentNullException();

            if (arrangerPosX > ArrangerElementSize.Width || arrangerPosY > ArrangerElementSize.Height)
                throw new ArgumentOutOfRangeException();

            ElementGrid[arrangerPosX, arrangerPosY] = element;
        }

        /// <summary>
        /// Gets an Element from a position in the Arranger ElementGrid
        /// </summary>
        /// <param name="arrangerPosX">0-based x-coordinate of Element</param>
        /// <param name="arrangerPosY">0-based y-coordinate of Element</param>
        /// <returns></returns>
        public ArrangerElement GetElement(int arrangerPosX, int arrangerPosY)
        {
            if (ElementGrid is null)
                throw new ArgumentNullException();

            return ElementGrid[arrangerPosX, arrangerPosY];
        }

        /// <summary>
        /// Tests the Arranger Elements to see if any Elements are blank
        /// </summary>
        /// <returns></returns>
        public bool ContainsBlankElements()
        {
            foreach(ArrangerElement el in ElementGrid)
            {
                if (el.IsBlank())
                    return true;
            }

            return false;
        }

        public override XElement Serialize()
        {
            throw new NotImplementedException();
        }

        public override bool Deserialize(XElement element)
        {
            throw new NotImplementedException();
        }
    }
}
