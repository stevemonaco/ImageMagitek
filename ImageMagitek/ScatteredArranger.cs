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
                throw new InvalidOperationException($"{nameof(Resize)} property '{nameof(Mode)}' is in invalid {nameof(ArrangerMode)} ({Mode.ToString()})");

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
    }
}
