using System;
using System.Drawing;
using System.Collections.Generic;
using ImageMagitek.Project;

namespace ImageMagitek
{
    public class ScatteredArranger: Arranger
    {
        public override bool ShouldBeSerialized { get; set; } = true;

        public ScatteredArranger()
        {
            Mode = ArrangerMode.ScatteredArranger;
        }

        /// <summary>
        /// Creates a new scattered arranger with default initialized elements
        /// </summary>
        /// <param name="layout">Layout type of the Arranger</param>
        /// <param name="arrangerWidth">Width of Arranger in Elements</param>
        /// <param name="arrangerHeight">Height of Arranger in Elements</param>
        /// <param name="elementWidth">Width of each element in pixels</param>
        /// <param name="elementHeight">Height of each element in pixels</param>
        /// <returns></returns>
        public ScatteredArranger(string name, ArrangerLayout layout, int arrangerWidth, int arrangerHeight, int elementWidth, int elementHeight)
        {
            Name = name;
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
        }

        public override Arranger CloneArranger()
        {
            if (Layout == ArrangerLayout.TiledArranger || Layout == ArrangerLayout.LinearArranger)
                return CloneArranger(0, 0, ArrangerPixelSize.Width, ArrangerPixelSize.Height);
            else
                throw new NotSupportedException($"{nameof(CloneArranger)} with {nameof(ArrangerLayout)} '{Layout}' is not supported");
        }

        /// <summary>
        /// Clones a subsection of the Arranger
        /// </summary>
        /// <param name="posX">Left edge of Arranger in pixel coordinates</param>
        /// <param name="posY">Top edge of Arranger in pixel coordinates</param>
        /// <param name="width">Width of Arranger in pixels</param>
        /// <param name="height">Height of Arranger in pixels</param>
        /// <returns></returns>
        public override Arranger CloneArranger(int posX, int posY, int width, int height)
        {
            if (posX < 0 || posX + width >= ArrangerPixelSize.Width || posY < 0 || posY + height >= ArrangerPixelSize.Height)
                throw new ArgumentOutOfRangeException($"{nameof(CloneArranger)} parameters ({nameof(posX)}: {posX}, {nameof(posY)}: {posY}, {nameof(width)}: {width}, {nameof(height)}: {height})" +
                    $" were outside of the bounds of arranger '{Name}' of size (width: {ArrangerPixelSize.Width}, height: {ArrangerPixelSize.Height})");

            if (Layout == ArrangerLayout.LinearArranger)
            {
                if (posX != 0 || posY != 0 || width != ArrangerPixelSize.Width || height != ArrangerPixelSize.Height)
                    throw new InvalidOperationException($"{nameof(CloneArranger)} of a LinearArranger must have the same dimensions as the original");
                return CloneArrangerInternal(posX, posY, width, height);
            }

            return CloneArrangerInternal(posX, posY, width, height);
        }

        private Arranger CloneArrangerInternal(int posX, int posY, int width, int height)
        {
            var elemX = posX / ElementPixelSize.Width;
            var elemY = posY / ElementPixelSize.Height;
            var elemsWidth = (width + ElementPixelSize.Width - 1) / ElementPixelSize.Width;
            var elemsHeight = (height + ElementPixelSize.Height - 1) / ElementPixelSize.Height;

            var arranger = new ScatteredArranger(Name, Layout, elemsWidth, elemsHeight, ElementPixelSize.Width, ElementPixelSize.Height);

            for (int y = 0; y < elemsHeight; y++)
            {
                for (int x = 0; x < elemsWidth; x++)
                {
                    var el = GetElement(x + elemX, y + elemY).Clone();
                    el.X1 = x * ElementPixelSize.Width;
                    el.Y1 = y * ElementPixelSize.Height;
                    arranger.SetElement(el, x, y);
                }
            }

            return arranger;
        }

        public override IEnumerable<IProjectResource> LinkedResources()
        {
            var set = new HashSet<IProjectResource>();

            foreach (var el in EnumerateElements())
            {
                set.Add(el.Palette);
                set.Add(el.DataFile);
            }

            foreach (var item in set)
                yield return item;
        }
    }
}
