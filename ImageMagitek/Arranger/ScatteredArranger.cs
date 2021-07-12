using System;
using System.Drawing;
using System.Collections.Generic;
using ImageMagitek.Project;
using ImageMagitek.Codec;
using System.Linq;

namespace ImageMagitek
{
    public class ScatteredArranger: Arranger
    {
        public override bool ShouldBeSerialized { get; set; } = true;

        /// <summary>
        /// Creates a new scattered arranger with default initialized elements
        /// </summary>
        /// <param name="layout">Layout type of the Arranger</param>
        /// <param name="arrangerWidth">Width of Arranger in Elements</param>
        /// <param name="arrangerHeight">Height of Arranger in Elements</param>
        /// <param name="elementWidth">Width of each element in pixels</param>
        /// <param name="elementHeight">Height of each element in pixels</param>
        /// <returns></returns>
        public ScatteredArranger(string name, PixelColorType colorType, ArrangerLayout layout, int arrangerWidth, int arrangerHeight, int elementWidth, int elementHeight)
        {
            Name = name;
            Mode = ArrangerMode.Scattered;
            Layout = layout;
            ColorType = colorType;

            if (Layout == ArrangerLayout.Single && (arrangerWidth != 1 || arrangerHeight != 1))
                throw new ArgumentException($"Arranger '{name}' with {ArrangerLayout.Single} does not have a width and height of 1");

            if (arrangerWidth <= 0 || arrangerHeight <= 0 || elementWidth <= 0 | elementHeight <= 0)
                throw new ArgumentOutOfRangeException($"Arranger '{name}' does not have positive sizes for arranger and elements");

            ElementGrid = new ArrangerElement?[arrangerHeight, arrangerWidth];
            ArrangerElementSize = new Size(arrangerWidth, arrangerHeight);
            ElementPixelSize = new Size(elementWidth, elementHeight);
        }

        /// <summary>
        /// Resizes a ScatteredArranger to the specified number of elements
        /// </summary>
        /// <param name="arrangerWidth">Width of Arranger in Elements</param>
        /// <param name="arrangerHeight">Height of Arranger in Elements</param>
        public override void Resize(int arrangerWidth, int arrangerHeight)
        {
            if (arrangerWidth < 1 || arrangerHeight < 1)
                throw new ArgumentOutOfRangeException($"{nameof(Resize)}: {nameof(arrangerWidth)} ({arrangerWidth}) and {nameof(arrangerHeight)} ({arrangerHeight}) must be larger than 0");

            if (Mode != ArrangerMode.Scattered)
                throw new InvalidOperationException($"{nameof(Resize)} property '{nameof(Mode)}' is in invalid {nameof(ArrangerMode)} ({Mode.ToString()})");

            var newGrid = new ArrangerElement?[arrangerWidth, arrangerHeight];

            int width = Math.Min(arrangerWidth, ArrangerElementSize.Width);
            int height = Math.Min(arrangerHeight, ArrangerElementSize.Height);

            for (int posY = 0; posY < height; posY++)
            {
                for (int posX = 0; posX < width; posX++)
                {
                    newGrid[posY, posX] = ElementGrid[posY, posX];
                }
            }

            ElementGrid = newGrid;
            ArrangerElementSize = new Size(arrangerWidth, arrangerHeight);
        }

        /// <summary>
        /// Private method for cloning an Arranger
        /// </summary>
        /// <param name="posX">Left edge of Arranger in pixel coordinates</param>
        /// <param name="posY">Top edge of Arranger in pixel coordinates</param>
        /// <param name="width">Width of Arranger in pixels</param>
        /// <param name="height">Height of Arranger in pixels</param>
        /// <returns></returns>
        protected override Arranger CloneArrangerCore(int posX, int posY, int width, int height)
        {
            var elemX = posX / ElementPixelSize.Width;
            var elemY = posY / ElementPixelSize.Height;
            var elemsWidth = (width + ElementPixelSize.Width - 1) / ElementPixelSize.Width;
            var elemsHeight = (height + ElementPixelSize.Height - 1) / ElementPixelSize.Height;

            var arranger = new ScatteredArranger(Name, ColorType, Layout, elemsWidth, elemsHeight, ElementPixelSize.Width, ElementPixelSize.Height);

            for (int y = 0; y < elemsHeight; y++)
            {
                for (int x = 0; x < elemsWidth; x++)
                {
                    var el = GetElement(x + elemX, y + elemY)?.WithLocation(x * ElementPixelSize.Width, y * ElementPixelSize.Height);
                    if (el is ArrangerElement element)
                        arranger.SetElement(element, x, y);
                }
            }

            return arranger;
        }

        public override IEnumerable<IProjectResource> LinkedResources
        {
            get
            {
                var set = new HashSet<IProjectResource>();

                foreach (var el in EnumerateElements().OfType<ArrangerElement>())
                {
                    if (el.Palette is object)
                        set.Add(el.Palette);

                    if (el.DataFile is object)
                        set.Add(el.DataFile);
                }

                return set;
            }
        }
    }
}
