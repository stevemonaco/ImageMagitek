using System;
using System.Collections.Generic;
using System.Drawing;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
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
    /// Specifies how the pixels' colors are determined for the graphic
    /// Indexed graphics have their full color determined by a palette
    /// Direct graphics have their full color determined by the pixel image data alone
    /// </summary>
    public enum PixelColorType { Indexed = 0, Direct }

    /// <summary>
    /// Move operations for sequential arrangers
    /// </summary>
    public enum ArrangerMoveType { ByteDown = 0, ByteUp, RowDown, RowUp, ColRight, ColLeft, PageDown, PageUp, Home, End, Absolute };

    /// <summary>
    /// Arranger for graphical screen elements
    /// </summary>
    public abstract class Arranger : IProjectResource
    {
        // General Arranger variables
        /// <summary>
        /// Gets individual Elements that compose the Arranger
        /// </summary>
        public ArrangerElement[,] ElementGrid { get; protected set; }

        /// <summary>
        /// Gets the size of the entire Arranger in Elements
        /// </summary>
        public Size ArrangerElementSize { get; protected set; }

        /// <summary>
        /// Gets the Size of the entire Arranger in unzoomed pixels
        /// </summary>
        public Size ArrangerPixelSize { get => new Size(ArrangerElementSize.Width * ElementPixelSize.Width, ArrangerElementSize.Height * ElementPixelSize.Height); }

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
        /// Gets the ColorType of the Arranger
        /// </summary>
        public PixelColorType ColorType { get; protected set; }

        public string Name { get; set; }

        public bool CanContainChildResources => false;

        public abstract bool ShouldBeSerialized { get; set; }

        /// <summary>
        /// Renames an Arranger to a new name
        /// </summary>
        /// <param name="name"></param>
        public void Rename(string name)
        {
            Name = name;
        }

        public abstract void Resize(int arrangerWidth, int arrangerHeight);
        public abstract Arranger CloneArranger();
        public abstract Arranger CloneArranger(int posX, int posY, int width, int height);

        /// <summary>
        /// Creates a new Scattered Arranger from an existing Arranger
        /// </summary>
        /// <param name="subArrangerName">Arranger name for the newly created Arranger</param>
        /// <param name="arrangerPosX">0-based top-most Element coordinate of parent Arranger selection to copy</param>
        /// <param name="arrangerPosY">0-based left-most Element coordinate of parent Arranger selection to copy</param>
        /// <param name="copyWidth">Width of selection to copy in Elements</param>
        /// <param name="copyHeight">Height of selection to copy in Elements</param>
        /// <returns></returns>
        //public virtual Arranger CreateSubArranger(string subArrangerName, int arrangerPosX, int arrangerPosY, int copyWidth, int copyHeight)
        //{
        //    if ((arrangerPosX < 0) || (arrangerPosX + copyWidth > ArrangerElementSize.Width))
        //        throw new ArgumentOutOfRangeException(nameof(arrangerPosX));

        //    if ((arrangerPosY < 0) || (arrangerPosY + copyHeight > ArrangerElementSize.Height))
        //        throw new ArgumentOutOfRangeException(nameof(arrangerPosY));

        //    Arranger subArranger = new ScatteredArranger()
        //    {
        //        Mode = ArrangerMode.ScatteredArranger, // Default to scattered arranger
        //        Name = subArrangerName,
        //        Layout = Layout,
        //        ElementGrid = new ArrangerElement[copyWidth, copyHeight],
        //        ArrangerElementSize = new Size(copyWidth, copyHeight),
        //        ElementPixelSize = ElementPixelSize,
        //    };

        //    for (int srcy = arrangerPosY, desty = 0; srcy < arrangerPosY + copyHeight; srcy++, desty++)
        //    {
        //        for (int srcx = arrangerPosX, destx = 0; srcx < arrangerPosX + copyWidth; srcx++, destx++)
        //        {
        //            ArrangerElement el = GetElement(srcx, srcy).Clone();
        //            el.X1 = destx * subArranger.ElementPixelSize.Width;
        //            el.Y1 = desty * subArranger.ElementPixelSize.Height;
        //            subArranger.SetElement(el, destx, desty);
        //        }
        //    }

        //    return subArranger;
        //}

        /// <summary>
        /// Sets Element to a position in the Arranger ElementGrid using a shallow copy
        /// </summary>
        /// <param name="element">Element to be placed into the ElementGrid</param>
        /// <param name="arrangerPosX">0-based x-coordinate of Element</param>
        /// <param name="arrangerPosY">0-based y-coordinate of Element</param>
        public void SetElement(ArrangerElement element, int arrangerPosX, int arrangerPosY)
        {
            if (ElementGrid is null)
                throw new NullReferenceException($"{nameof(SetElement)} property '{nameof(ElementGrid)}' was null");

            if (arrangerPosX > ArrangerElementSize.Width || arrangerPosY > ArrangerElementSize.Height)
                throw new ArgumentOutOfRangeException($"{nameof(SetElement)} parameter was out of range: ({arrangerPosX}, {arrangerPosY})");

            ElementGrid[arrangerPosX, arrangerPosY] = element;
        }

        /// <summary>
        /// Gets an Element from a position in the Arranger ElementGrid in element coordinates
        /// </summary>
        /// <param name="arrangerPosX">0-based x-coordinate of Element</param>
        /// <param name="arrangerPosY">0-based y-coordinate of Element</param>
        /// <returns></returns>
        public ArrangerElement GetElement(int arrangerPosX, int arrangerPosY)
        {
            if (ElementGrid is null)
                throw new NullReferenceException($"{nameof(GetElement)} property '{nameof(ElementGrid)}' was null");

            if (arrangerPosX >= ArrangerElementSize.Width || arrangerPosY >= ArrangerElementSize.Height)
                throw new ArgumentOutOfRangeException($"{nameof(SetElement)} parameter was out of range: ({arrangerPosX}, {arrangerPosY})");

            return ElementGrid[arrangerPosX, arrangerPosY];
        }

        public ArrangerElement GetElementAtPixel(int arrangerPosX, int arrangerPosY)
        {
            if (ElementGrid is null)
                throw new NullReferenceException($"{nameof(GetElementAtPixel)} property '{nameof(ElementGrid)}' was null");

            if (arrangerPosX >= ArrangerPixelSize.Width || arrangerPosY >= ArrangerPixelSize.Height)
                throw new ArgumentOutOfRangeException($"{nameof(SetElement)} parameter was out of range: ({arrangerPosX}, {arrangerPosY})");

            return ElementGrid[arrangerPosX / ElementPixelSize.Width, arrangerPosY / ElementPixelSize.Height];
        }

        public IEnumerable<ArrangerElement> EnumerateElements()
        {
            for (int y = 0; y < ArrangerElementSize.Height; y++)
                for (int x = 0; x < ArrangerElementSize.Width; x++)
                    yield return ElementGrid[x, y];
        }

        public HashSet<Palette> GetReferencedPalettes()
        {
            var set = new HashSet<Palette>();

            for (int y = 0; y < ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < ArrangerElementSize.Width; x++)
                {
                    if (ElementGrid[x, y].Palette is object)
                        set.Add(ElementGrid[x, y].Palette);
                }
            }

            return set;
        }

        public HashSet<IGraphicsCodec> GetReferencedCodecs()
        {
            var set = new HashSet<IGraphicsCodec>();

            for (int y = 0; y < ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < ArrangerElementSize.Width; x++)
                {
                    if (ElementGrid[x, y].Codec is BlankCodec)
                        continue;

                    set.Add(ElementGrid[x, y].Codec);
                }
            }

            return set;
        }

        public abstract IEnumerable<IProjectResource> LinkedResources();
    }
}
